using Microsoft.Diagnostics.Runtime.MacOS.Structs;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Diagnostics.Runtime.MacOS
{
    internal sealed unsafe class MachOModule
    {
        private readonly MachOCoreDump _parent;

        public ulong BaseAddress { get; }
        public string FileName { get; }

        private readonly MachHeader64 _header;
        private readonly SymtabCommand _symtab;
        private readonly DysymtabCommand _dysymtab;
        private readonly MachOSegment[] _segments;
        private readonly ulong _stringTableAddress;
        private volatile NList64[]? _symTable;

        internal MachOModule(MachOCoreDump parent, ulong address, string path)
            :this(parent, parent.ReadMemory<MachHeader64>(address), address, path)
        {
        }

        internal MachOModule(MachOCoreDump parent, in MachHeader64 header, ulong address, string path)
        {
            BaseAddress = address;
            FileName = path;
            _parent = parent;
            _header = header;

            if (header.Magic != MachHeader64.Magic64)
                throw new InvalidDataException($"Module at {address:x} does not contain the expected Mach-O header.");

            List<MachOSegment> segments = new List<MachOSegment>((int)_header.NumberCommands);

            uint offset = (uint)sizeof(MachHeader64);
            for (int i = 0; i < _header.NumberCommands; i++)
            {
                ulong cmdAddress = BaseAddress + offset;
                LoadCommandHeader cmd = _parent.ReadMemory<LoadCommandHeader>(cmdAddress);

                switch (cmd.Kind)
                {
                    case LoadCommandType.Segment64:
                        Segment64LoadCommand seg64LoadCmd = _parent.ReadMemory<Segment64LoadCommand>(cmdAddress + (uint)sizeof(LoadCommandHeader));
                        segments.Add(new MachOSegment(seg64LoadCmd));
                        break;

                    case LoadCommandType.SymTab:
                        _symtab = _parent.ReadMemory<SymtabCommand>(cmdAddress);
                        break;

                    case LoadCommandType.DysymTab:
                        _dysymtab = _parent.ReadMemory<DysymtabCommand>(cmdAddress);
                        break;
                }

                offset += cmd.Size;
            }

            segments.Sort((x, y) => x.Address.CompareTo(y.Address));
            _segments = segments.ToArray();

            if (_symtab.StrOff != 0)
                _stringTableAddress = GetAddressFromFileOffset(_symtab.StrOff);
        }

        public bool TryLookupSymbol(string symbol, out ulong address)
        {
            if (symbol is null)
                throw new ArgumentNullException(nameof(symbol));

            address = 0;

            if (_stringTableAddress == 0)
                return false;

            NList64[]? symTable = ReadSymbolTable();
            if (symTable is null)
                return false;

            for (int i = 0; i < _dysymtab.nextdefsym; i++)
            {
                string name = GetSymbolName(symTable[i], symbol.Length + 1);
                if (name == symbol)
                {
                    address = BaseAddress + symTable[i].n_value;
                    return true;
                }
            }

            return false;
        }

        private string GetSymbolName(NList64 tableEntry, int max)
        {
            ulong nameOffset = _stringTableAddress + tableEntry.n_strx;
            return _parent.ReadAscii(nameOffset, max);
        }

        private NList64[]? ReadSymbolTable()
        {
            if (_symTable != null)
                return _symTable;

            if (_dysymtab.cmd != LoadCommandType.DysymTab || _symtab.Kind != LoadCommandType.SymTab)
                return null;

            ulong symbolTableAddress = GetAddressFromFileOffset(_symtab.SymOff) + _dysymtab.iextdefsym * (uint)sizeof(NList64);

            NList64[] symTable = new NList64[_dysymtab.iextdefsym];

            int count;
            fixed (NList64* ptr = symTable)
                count = _parent.ReadMemory(symbolTableAddress, new Span<byte>(ptr, symTable.Length * sizeof(NList64))) / sizeof(NList64);

            _symTable = symTable;
            return symTable;
        }

        private ulong GetAddressFromFileOffset(uint fileOffset)
        {
            foreach (var seg in _segments)
                if (seg.FileOffset <= fileOffset && fileOffset < seg.FileOffset + seg.FileSize)
                    return BaseAddress + fileOffset + seg.Address - seg.FileOffset;

            return 0;
        }

        public IEnumerable<Segment64LoadCommand> EnumerateSegments(string name)
        {
            uint offset = MachHeader64.Size;
            for (int i = 0; i < _header.NumberCommands; i++)
            {
                ulong cmdAddress = BaseAddress + offset;
                LoadCommandHeader cmd = _parent.ReadMemory<LoadCommandHeader>(cmdAddress);

                if (cmd.Kind == LoadCommandType.Segment64)
                {
                    Segment64LoadCommand seg64 = _parent.ReadMemory<Segment64LoadCommand>(cmdAddress + LoadCommandHeader.HeaderSize);
                    if (seg64.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        yield return seg64;
                }

                offset += cmd.Size;
            }
        }
    }
}
