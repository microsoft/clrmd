// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A helper class to represent an ELF note section.
    /// </summary>
    internal sealed class ElfNote
    {
        private readonly Reader _reader;
        private readonly ulong _position;
        private string? _name;

        internal ElfNoteHeader Header { get; }

        /// <summary>
        /// The content size of the data stored within this note.
        /// </summary>
        public int ContentSize => Header.ContentSize > int.MaxValue ? int.MaxValue : (int)Header.ContentSize;

        /// <summary>
        /// The type of note this is.
        /// </summary>
        public ElfNoteType Type => Header.Type;

        /// <summary>
        /// The note's name.
        /// </summary>
        public string Name
        {
            get
            {
                if (_name != null)
                    return _name;

                ulong namePosition = _position + HeaderSize;
                _name = _reader.ReadNullTerminatedAscii(namePosition, (int)Header.NameSize);
                return _name;
            }
        }

        internal ulong TotalSize => HeaderSize + Align4(Header.NameSize) + Align4(Header.ContentSize);

        private static unsafe uint HeaderSize => (uint)sizeof(ElfNoteHeader);

        /// <summary>
        /// Reads the contents of this note file.
        /// </summary>
        /// <param name="position">The position within the note to read from.</param>
        /// <param name="buffer">The buffer to read the note into.</param>
        /// <returns>The number of bytes read written to buffer.</returns>
        public int ReadContents(ulong position, Span<byte> buffer)
        {
            ulong contentsoffset = _position + HeaderSize + Align4(Header.NameSize);
            return _reader.ReadBytes(position + contentsoffset, buffer);
        }

        internal T ReadContents<T>(ref ulong position)
            where T : unmanaged
        {
            ulong contentsOffset = _position + HeaderSize + Align4(Header.NameSize);
            ulong locationOrig = contentsOffset + position;
            ulong location = locationOrig;
            T result = _reader.Read<T>(ref location);

            position += location - locationOrig;
            return result;
        }

        /// <summary>
        /// Reads the contents of this note file.
        /// </summary>
        /// <param name="position">The position within the note to read from.</param>
        /// <exception cref="System.IO.IOException">If the data could not be read.</exception>
        /// <returns>The data at the given position.</returns>
        public T ReadContents<T>(ulong position)
            where T : unmanaged
        {
            ulong contentsOffset = _position + HeaderSize + Align4(Header.NameSize);
            ulong location = contentsOffset + position;
            T result = _reader.Read<T>(location);
            return result;
        }

        internal ElfNote(Reader reader, ulong position)
        {
            _position = position;
            _reader = reader;

            Header = _reader.Read<ElfNoteHeader>(_position);
        }

        public static void CopyFloatingPoint(Architecture architecture, ElfNoteType type, ReadOnlySpan<byte> registers, uint contextFlags, Span<byte> context)
        {
            uint requested = contextFlags == 0 ? uint.MaxValue : contextFlags;
            uint floatingPoint = ThreadContext.FloatingPointFlag(architecture);
            if ((requested & floatingPoint) == 0 &&
                !(architecture == Architecture.X86 && (requested & (X86Context.ContextExtendedRegisters & ~X86Context.Context)) != 0))
                return;

            uint flags = floatingPoint;
            switch (architecture)
            {
                case Architecture.X64 when (type is ElfNoteType.PrpsFpreg or ElfNoteType.X86XState) && registers.Length >= 512:
                    registers.Slice(0, 512).CopyTo(context.Slice(0x100));
                    registers.Slice(24, 4).CopyTo(context.Slice(0x34));
                    break;

                case Architecture.X86 when type == ElfNoteType.PrpsFpreg && registers.Length >= 108 && (requested & floatingPoint) != 0:
                    registers.Slice(0, 108).CopyTo(context.Slice(0x1c));
                    context.Slice(0x88, 4).Clear();
                    break;

                case Architecture.X86 when (type is ElfNoteType.PrXfpreg or ElfNoteType.X86XState) && registers.Length >= 512:
                    if ((requested & (X86Context.ContextExtendedRegisters & ~X86Context.Context)) != 0)
                        registers.Slice(0, 512).CopyTo(context.Slice(0xcc));
                    if ((requested & floatingPoint) != 0)
                        CopyX86FloatingPoint(registers, context);
                    flags = (X86Context.ContextFloatingPoint | X86Context.ContextExtendedRegisters) & requested & ~X86Context.Context;
                    break;

                case Architecture.Arm when type == ElfNoteType.ArmVfp && registers.Length >= 260:
                    registers.Slice(0, 256).CopyTo(context.Slice(0x50));
                    registers.Slice(256, 4).CopyTo(context.Slice(0x48));
                    break;

                case Architecture.Arm64 when type == ElfNoteType.PrpsFpreg && registers.Length >= 520:
                    registers.Slice(0, 512).CopyTo(context.Slice(0x110));
                    registers.Slice(512, 4).CopyTo(context.Slice(0x314));
                    registers.Slice(516, 4).CopyTo(context.Slice(0x310));
                    break;

                case (Architecture)9 when type == ElfNoteType.PrpsFpreg && registers.Length >= 260:
                    registers.Slice(0, 260).CopyTo(context.Slice(0x110));
                    break;

                case (Architecture)6 when type == ElfNoteType.PrpsFpreg && registers.Length >= 268:
                    context.Slice(0x110, 1024).Clear();
                    for (int i = 0; i < 32; i++)
                        registers.Slice(i * 8, 8).CopyTo(context.Slice(0x110 + i * 32));
                    registers.Slice(256, 12).CopyTo(context.Slice(0x510));
                    break;

                case (Architecture)6 when type is ElfNoteType.LoongArchLsx or ElfNoteType.LoongArchLasx:
                    if ((ThreadContext.GetFlags(architecture, context) & floatingPoint) == 0)
                        return;
                    int width = type == ElfNoteType.LoongArchLsx ? 16 : 32;
                    if (registers.Length < 32 * width)
                        return;
                    for (int i = 0; i < 32; i++)
                        registers.Slice(i * width, width).CopyTo(context.Slice(0x110 + i * 32));
                    // Vector notes supplement NT_FPREGSET; they do not contain FCSR/FCC.
                    break;

                default:
                    return;
            }

            ThreadContext.SetFlags(architecture, context, ThreadContext.GetFlags(architecture, context) | flags);
        }

        private static void CopyX86FloatingPoint(ReadOnlySpan<byte> registers, Span<byte> context)
        {
            ref X86Context result = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, X86Context>(context));
            result.ControlWord = MemoryMarshal.Read<ushort>(registers);
            result.StatusWord = MemoryMarshal.Read<ushort>(registers.Slice(2));
            result.ErrorOffset = MemoryMarshal.Read<uint>(registers.Slice(8));
            result.ErrorSelector = MemoryMarshal.Read<ushort>(registers.Slice(12)) |
                ((uint)MemoryMarshal.Read<ushort>(registers.Slice(6)) << 16);
            result.DataOffset = MemoryMarshal.Read<uint>(registers.Slice(16));
            result.DataSelector = MemoryMarshal.Read<ushort>(registers.Slice(20));
            result.TagWord = 0;
            result.Cr0NpxState = 0;
            int top = (int)(result.StatusWord >> 11) & 7;
            for (int i = 0; i < 8; i++)
            {
                registers.Slice(32 + i * 16, 10).CopyTo(context.Slice(0x38 + i * 10));
                int slot = (i - top + 8) & 7;
                ulong significand = MemoryMarshal.Read<ulong>(registers.Slice(32 + slot * 16));
                int exponent = MemoryMarshal.Read<ushort>(registers.Slice(40 + slot * 16)) & 0x7fff;
                uint tag = (registers[4] & (1 << i)) == 0 ? 3u :
                    exponent == 0 && significand == 0 ? 1u :
                    exponent == 0 || exponent == 0x7fff || (significand & (1ul << 63)) == 0 ? 2u : 0u;
                result.TagWord |= tag << (i * 2);
            }
        }

        private static uint Align4(uint x)
        {
            return (x + 3U) & ~3U;
        }
    }
}