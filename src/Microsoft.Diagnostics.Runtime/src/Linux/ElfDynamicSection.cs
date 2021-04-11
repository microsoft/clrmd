// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal class ElfDynamicSection
    {
        public ElfDynamicSection(Reader reader, bool is64Bit, ulong address, ulong size)
        {
            ulong endAddress = address + size;
            while (address < endAddress)
            {
                ElfDynamicEntryTag tag;
                ulong ptr;
                if (is64Bit)
                {
                    ElfDynamicEntry64 dyn = reader.Read<ElfDynamicEntry64>(ref address);
                    tag = (ElfDynamicEntryTag)dyn.Tag;
                    ptr = dyn.Ptr;
                }
                else
                {
                    ElfDynamicEntry32 dyn = reader.Read<ElfDynamicEntry32>(ref address);
                    tag = (ElfDynamicEntryTag)dyn.Tag;
                    ptr = dyn.Ptr;
                }
                if (tag == ElfDynamicEntryTag.Null)
                {
                    break;
                }
                else if (tag == ElfDynamicEntryTag.GnuHash)
                {
                    GnuHashTableVA = ptr;
                }
                else if (tag == ElfDynamicEntryTag.StrTab)
                {
                    StringTableVA = ptr;
                }
                else if (tag == ElfDynamicEntryTag.SymTab)
                {
                    SymbolTableVA = ptr;
                }
                else if (tag == ElfDynamicEntryTag.StrSz)
                {
                    StringTableSize = ptr;
                }
            }

            if (StringTableVA == 0 || StringTableSize == 0)
            {
                throw new InvalidDataException("The ELF dump string table is invalid");
            }

            if (SymbolTableVA == 0 || GnuHashTableVA == 0)
            {
                throw new InvalidDataException("The ELF dump symbol or hash table is invalid");
            }

            // In a loaded image the loader does fixups so StringTableVA and StringTableSize now store final VAs
            StringTable = new ElfStringTable(reader, StringTableVA, StringTableSize);
            SymbolTable = new ElfSymbolTable(reader, is64Bit, SymbolTableVA, StringTable);
            GnuHash = new ElfSymbolGnuHash(reader, is64Bit, GnuHashTableVA);
        }

        public ulong GnuHashTableVA { get; }

        public ElfSymbolGnuHash GnuHash { get; }

        public ulong StringTableVA { get; }

        public ulong StringTableSize { get; }

        public ElfStringTable StringTable { get; }

        public ulong SymbolTableVA { get; }

        public ElfSymbolTable SymbolTable {get; }

        public bool TryLookupSymbol(string symbolName, out ElfSymbol? symbol)
        {
            if (GnuHash == null || SymbolTable == null || StringTable == null)
            {
                symbol = null;
                return false;
            }

            foreach (uint possibleLocation in GnuHash.GetPossibleSymbolIndex(symbolName))
            {
                ElfSymbol s = SymbolTable.GetSymbol(possibleLocation);
                if(s.Name == symbolName)
                {
                    symbol = s;
                    return true;
                }
            }
            symbol = null;
            return false;
        }
    }
}