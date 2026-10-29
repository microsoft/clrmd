// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.MacOS;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;
using Native = Microsoft.Diagnostics.Runtime.MacOS.MacOSProcessDataReader.Native;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ThreadContextTests
    {
        private const ulong RegisterValue = 0x1122334455667788;
        private const ulong Untouched = 0xa5a5a5a5a5a5a5a5;

        [Theory]
        [InlineData(Architecture.X64, 0x4d0)]
        [InlineData(Architecture.X86, 0x2cc)]
        [InlineData(Architecture.Arm, 0x1a0)]
        [InlineData(Architecture.Arm64, 0x390)]
        [InlineData((Architecture)9, 0x220)]
        [InlineData((Architecture)6, 0x520)]
        public void ElfCoreReadsGeneralAndFloatingPointNotes(Architecture architecture, int size)
        {
            using MemoryStream stream = CreateElfCore(architecture, includeFloatingPoint: true);
            using CoredumpReader reader = new("test.core", stream, leaveOpen: false);
            byte[] context = new byte[size];
            uint flags = ThreadContext.ArchitectureFlag(architecture) |
                (architecture is Architecture.X64 or Architecture.X86 ? 0xfu : 7u);

            Assert.True(reader.GetThreadContext(42, flags, context));
            Assert.Equal(flags, ThreadContext.GetFlags(architecture, context));
            AssertGeneralRegisters(architecture, context);

            int fp = architecture switch
            {
                Architecture.X64 => 0x200,
                Architecture.X86 => 0x38,
                Architecture.Arm => 0x50,
                _ => 0x110
            };
            Assert.Equal(RegisterValue, Read<ulong>(context, fp));
            if (architecture == Architecture.X64)
            {
                Assert.Equal(3.5, Read<double>(context, 0x210)); // XMM7
                Assert.Equal(0x1f80u, Read<uint>(context, 0x34));
                Assert.Equal(0x246, Read<int>(context, 0x44));
                Assert.Equal(0x11110008ul, Read<ulong>(context, 0xc0)); // R9
                Assert.Equal(0x11110009ul, Read<ulong>(context, 0xb8)); // R8
            }
            else if (architecture == Architecture.Arm64)
            {
                Assert.Equal(0x1234u, Read<uint>(context, 0x314));
                Assert.Equal(0x5678u, Read<uint>(context, 0x310));
            }
            else if (architecture == (Architecture)6)
            {
                Assert.Equal(RegisterValue + 1, Read<ulong>(context, 0x128)); // LASX upper lane
                Assert.Equal(0x1234ul, Read<ulong>(context, 0x510));
                Assert.Equal(0x5678u, Read<uint>(context, 0x518));
            }

            // FP notes following the next PRSTATUS must not be associated with the previous thread.
            Assert.True(reader.GetThreadContext(43, flags, context));
            Assert.Equal(RegisterValue + 2, Read<ulong>(context, fp));

            context.AsSpan().Fill(0xa5);
            Assert.True(reader.GetThreadContext(42, ThreadContext.ArchitectureFlag(architecture) | 1, context));
            Assert.Equal(Untouched, Read<ulong>(context, fp));
            if (architecture == Architecture.X64)
                Assert.Equal(0x11110004ul, Read<ulong>(context, 0xa0));
            int integer = architecture switch
            {
                Architecture.X64 => 0x78,
                Architecture.X86 => 0xb0,
                Architecture.Arm => 4,
                Architecture.Arm64 => 8,
                (Architecture)9 => 0x20,
                _ => 0x28
            };
            Assert.Equal(0xa5a5a5a5u, Read<uint>(context, integer));
            if (architecture is (Architecture)9 or (Architecture)6)
                Assert.Equal(0x11110001ul, Read<ulong>(context, 0x10)); // Ra is a control register.

            context.AsSpan().Fill(0xa5);
            Assert.True(reader.GetThreadContext(42, ThreadContext.ArchitectureFlag(architecture) | 2, context));
            Assert.Equal(Untouched, Read<ulong>(context, fp));
            if (architecture == Architecture.X64)
                Assert.Equal(Untouched, Read<ulong>(context, 0xa0));
            Assert.NotEqual(0xa5a5a5a5u, Read<uint>(context, integer));
            int pc = architecture switch
            {
                Architecture.X64 => 0xf8,
                Architecture.X86 => 0xb8,
                Architecture.Arm => 0x40,
                _ => 0x108
            };
            Assert.Equal(0xa5a5a5a5u, Read<uint>(context, pc));
            if (architecture is (Architecture)9 or (Architecture)6)
                Assert.Equal(Untouched, Read<ulong>(context, 0x10));
        }

        [Fact]
        public void ElfCoreDoesNotClaimMissingFloatingPointState()
        {
            using MemoryStream stream = CreateElfCore(Architecture.X64, includeFloatingPoint: false);
            using CoredumpReader reader = new("test.core", stream, leaveOpen: false);
            byte[] context = Enumerable.Repeat((byte)0xa5, AMD64Context.Size).ToArray();
            Assert.True(reader.GetThreadContext(42, AMD64Context.Context | 0xb, context));
            Assert.Equal(AMD64Context.Context | 3, ThreadContext.GetFlags(Architecture.X64, context));
            Assert.Equal(Untouched, Read<ulong>(context, 0x200));
            Assert.True(reader.GetThreadContext(42, 0, context));
            Assert.Equal(AMD64Context.Context | 7, ThreadContext.GetFlags(Architecture.X64, context));
        }

        [Fact]
        public void CopyPreservesContextStatusWithoutClaimingUnavailableGroups()
        {
            byte[] source = new byte[AMD64Context.Size];
            byte[] destination = Enumerable.Repeat((byte)0xa5, source.Length).ToArray();
            ThreadContext.SetFlags(Architecture.X64, source, AMD64Context.ContextControl | 0x20000000);
            Assert.True(ThreadContext.Copy(Architecture.X64, AMD64Context.Context | 0xb, source, destination));
            Assert.Equal(AMD64Context.ContextControl | 0x20000000, ThreadContext.GetFlags(Architecture.X64, destination));
            Assert.Equal(Untouched, Read<ulong>(destination, 0x200));
        }

        [Fact]
        public void X86ExtendedRegistersAreIndependentOfLegacyFloatingPointGroup()
        {
            using MemoryStream stream = CreateElfCore(Architecture.X86, includeFloatingPoint: true);
            using CoredumpReader reader = new("test.core", stream, leaveOpen: false);
            byte[] context = Enumerable.Repeat((byte)0xa5, X86Context.Size).ToArray();
            Assert.True(reader.GetThreadContext(42, X86Context.ContextExtendedRegisters, context));
            Assert.Equal(X86Context.ContextExtendedRegisters, Read<uint>(context, 0));
            Assert.Equal(RegisterValue, Read<ulong>(context, 0x1cc)); // XMM6
            Assert.Equal(Untouched, Read<ulong>(context, 0x38));
            Assert.Equal(0xa5a5a5a5u, Read<uint>(context, 0xb0));
        }

        [Fact]
        public void X86FxsaveReconstructsPhysicalX87Tags()
        {
            byte[] registers = new byte[512];
            Write(registers, 2, (ushort)(3 << 11)); // TOP = 3
            registers[4] = 0x38; // Physical registers 3, 4 and 5 are nonempty.
            Write(registers, 32, 1ul << 63);
            Write(registers, 40, (ushort)0x3fff); // ST0 = 1
            Write(registers, 64, 1ul << 63);
            Write(registers, 72, (ushort)0x7fff); // ST2 = infinity
            byte[] context = new byte[X86Context.Size];
            ThreadContext.SetFlags(Architecture.X86, context, X86Context.Context);

            ElfNote.CopyFloatingPoint(Architecture.X86, ElfNoteType.PrXfpreg, registers, 0, context);
            X86Context result = MemoryMarshal.Read<X86Context>(context);
            Assert.Equal(0xf93fu, result.TagWord);
            Assert.Equal(1ul << 63, Read<ulong>(context, 0x38));
            Assert.Equal(0ul, Read<ulong>(context, 0x42));
            Assert.Equal(1ul << 63, Read<ulong>(context, 0x4c));
            byte[] expected = context.ToArray();
            ElfNote.CopyFloatingPoint(Architecture.X86, ElfNoteType.PrXfpreg, registers.AsSpan(0, 511), 0, context);
            Assert.Equal(expected, context);
        }

        [Theory]
        [InlineData(Architecture.X64, Native.x86_FLOAT_STATE64)]
        [InlineData(Architecture.X64, Native.x86_AVX_STATE64)]
        [InlineData(Architecture.X64, Native.x86_AVX512_STATE64)]
        [InlineData(Architecture.Arm64, Native.ARM_NEON_STATE64)]
        public void MachCoreReadsEveryThreadStateFlavor(Architecture architecture, uint floatingPointFlavor)
        {
            using MemoryStream stream = CreateMachCore(architecture, floatingPointFlavor);
            using MachOCoreReader reader = new("test.core", stream, leaveOpen: false);
            byte[] context = Enumerable.Repeat((byte)0xa5, architecture == Architecture.X64 ? AMD64Context.Size : Arm64Context.Size).ToArray();
            uint flags = ThreadContext.ArchitectureFlag(architecture) | ThreadContext.FloatingPointFlag(architecture);
            Assert.True(reader.GetThreadContext(0, flags, context));
            Assert.Equal(flags, ThreadContext.GetFlags(architecture, context));
            Assert.Equal(RegisterValue, Read<ulong>(context, architecture == Architecture.X64 ? 0x200 : 0x110));
            Assert.Equal(Untouched, Read<ulong>(context, architecture == Architecture.X64 ? 0x78 : 8));

            Assert.True(reader.GetThreadContext(0, 0, context));
            Assert.Equal(RegisterValue + 1, Read<ulong>(context, architecture == Architecture.X64 ? 0x78 : 8));
            Assert.Equal(RegisterValue + 2, Read<ulong>(context, architecture == Architecture.X64 ? 0x48 : 0x338));

            if (architecture == Architecture.X64)
            {
                context.AsSpan().Fill(0xa5);
                Assert.True(reader.GetThreadContext(0, AMD64Context.ContextControl, context));
                Assert.Equal(AMD64Context.ContextControl, ThreadContext.GetFlags(architecture, context));
                Assert.Equal(RegisterValue + 3, Read<ulong>(context, 0xa0));
                Assert.Equal(Untouched, Read<ulong>(context, 0x78));

                context.AsSpan().Fill(0xa5);
                Assert.True(reader.GetThreadContext(0, AMD64Context.ContextInteger, context));
                Assert.Equal(AMD64Context.ContextInteger, ThreadContext.GetFlags(architecture, context));
                Assert.Equal(Untouched, Read<ulong>(context, 0xa0));
                Assert.Equal(RegisterValue + 1, Read<ulong>(context, 0x78));
            }
        }

        [Fact]
        public void MachCoreRejectsThreadStateOutsideLoadCommand()
        {
            using MemoryStream stream = CreateMachCore(Architecture.X64);
            stream.Position = 44;
            using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                writer.Write(uint.MaxValue);
            stream.Position = 0;
            Assert.Throws<InvalidDataException>(() => new MachOCoreReader("test.core", stream, leaveOpen: true));
        }

        private static void AssertGeneralRegisters(Architecture architecture, byte[] context)
        {
            switch (architecture)
            {
                case Architecture.X64:
                    Assert.Equal(0x1111000aul, Read<ulong>(context, 0x78));
                    Assert.Equal(0x11110010ul, Read<ulong>(context, 0xf8));
                    break;
                case Architecture.X86:
                    Assert.Equal(0x11110006u, Read<uint>(context, 0xb0));
                    Assert.Equal(0x1111000cu, Read<uint>(context, 0xb8));
                    Assert.Equal(0x1111000du, Read<uint>(context, 0xbc));
                    break;
                case Architecture.Arm:
                    Assert.Equal(0x11110000u, Read<uint>(context, 4));
                    Assert.Equal(0x1111000fu, Read<uint>(context, 0x40));
                    break;
                case Architecture.Arm64:
                    Assert.Equal(0x11110000ul, Read<ulong>(context, 8));
                    Assert.Equal(0x11110020ul, Read<ulong>(context, 0x108));
                    break;
                case (Architecture)9:
                    Assert.Equal(0ul, Read<ulong>(context, 8));
                    Assert.Equal(0x11110001ul, Read<ulong>(context, 0x10));
                    Assert.Equal(0x11110000ul, Read<ulong>(context, 0x108));
                    break;
                case (Architecture)6:
                    Assert.Equal(0x11110001ul, Read<ulong>(context, 0x10));
                    Assert.Equal(0x11110021ul, Read<ulong>(context, 0x108));
                    break;
            }
        }

        private static MemoryStream CreateElfCore(Architecture architecture, bool includeFloatingPoint)
        {
            bool is32 = architecture is Architecture.X86 or Architecture.Arm;
            int wordSize = is32 ? 4 : 8;
            int registerOffset = is32 ? 72 : 112;
            int registerCount = architecture switch
            {
                Architecture.X64 => 27,
                Architecture.X86 => 17,
                Architecture.Arm => 18,
                Architecture.Arm64 => 34,
                (Architecture)9 => 32,
                (Architecture)6 => 45,
                _ => throw new ArgumentOutOfRangeException(nameof(architecture))
            };
            using MemoryStream notes = new();
            using (BinaryWriter writer = new(notes, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                WriteNote(writer, ElfNoteType.File, new byte[is32 ? 8 : 16]);
                for (uint tid = 42; tid <= 43; tid++)
                {
                    byte[] status = new byte[registerOffset + registerCount * wordSize + 4];
                    Write(status, is32 ? 24 : 32, tid);
                    for (int i = 0; i < registerCount; i++)
                    {
                        if (is32)
                            Write(status, registerOffset + i * wordSize, 0x11110000u + (uint)i);
                        else
                            Write(status, registerOffset + i * wordSize, 0x11110000ul + (uint)i);
                    }
                    if (architecture == Architecture.X64)
                    {
                        Write(status, registerOffset + 18 * 8, 0x246ul);
                        foreach (int selector in new[] { 17, 20, 23, 24, 25, 26 })
                            Write(status, registerOffset + selector * 8, 0x33ul);
                    }
                    WriteNote(writer, ElfNoteType.PrpsStatus, status);
                    if (!includeFloatingPoint)
                        continue;

                    ulong value = RegisterValue + (tid - 42) * 2;
                    byte[] fp = new byte[architecture switch
                    {
                        Architecture.X64 or Architecture.X86 => 512,
                        Architecture.Arm or (Architecture)9 => 260,
                        Architecture.Arm64 => 528,
                        _ => 272
                    }];
                    Write(fp, 0, value);
                    ElfNoteType type = ElfNoteType.PrpsFpreg;
                    if (architecture is Architecture.X64 or Architecture.X86)
                    {
                        Write(fp, 24, 0x1f80u);
                        Write(fp, 32, value);
                        Write(fp, 256, value); // XMM6
                        Write(fp, 272, 3.5); // XMM7
                        if (architecture == Architecture.X86)
                            type = ElfNoteType.PrXfpreg;
                    }
                    else if (architecture == Architecture.Arm)
                    {
                        type = ElfNoteType.ArmVfp;
                    }
                    else if (architecture == Architecture.Arm64)
                    {
                        Write(fp, 512, 0x1234u);
                        Write(fp, 516, 0x5678u);
                    }
                    else if (architecture == (Architecture)6)
                    {
                        Write(fp, 256, 0x1234ul);
                        Write(fp, 264, 0x5678u);
                    }
                    WriteNote(writer, type, fp);
                    if (architecture == (Architecture)6)
                    {
                        byte[] vectors = new byte[1024];
                        Write(vectors, 0, value);
                        Write(vectors, 24, value + 1);
                        WriteNote(writer, ElfNoteType.LoongArchLasx, vectors);
                    }
                }
            }

            MemoryStream stream = new();
            using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F', is32 ? (byte)1 : (byte)2, 1, 1, 0 });
                writer.Write(0ul);
                writer.Write((ushort)4);
                writer.Write((ushort)(architecture switch
                {
                    Architecture.X64 => 62,
                    Architecture.X86 => 3,
                    Architecture.Arm => 40,
                    Architecture.Arm64 => 183,
                    (Architecture)9 => 243,
                    _ => 258
                }));
                writer.Write(1u);
                if (is32)
                {
                    writer.Write(0u);
                    writer.Write(52u);
                    writer.Write(0u);
                }
                else
                {
                    writer.Write(0ul);
                    writer.Write(64ul);
                    writer.Write(0ul);
                }
                writer.Write(0u);
                writer.Write((ushort)(is32 ? 52 : 64));
                writer.Write((ushort)(is32 ? 32 : 56));
                writer.Write((ushort)1);
                writer.Write(new byte[6]);
                writer.Write(4u); // PT_NOTE
                if (is32)
                {
                    writer.Write(84u);
                    writer.Write(0ul);
                    writer.Write((uint)notes.Length);
                    writer.Write((uint)notes.Length);
                    writer.Write(0ul);
                }
                else
                {
                    writer.Write(0u);
                    writer.Write(120ul);
                    writer.Write(0ul);
                    writer.Write(0ul);
                    writer.Write((ulong)notes.Length);
                    writer.Write((ulong)notes.Length);
                    writer.Write(0ul);
                }
                writer.Write(notes.ToArray());
            }
            stream.Position = 0;
            return stream;
        }

        private static void WriteNote(BinaryWriter writer, ElfNoteType type, byte[] contents)
        {
            writer.Write(5u);
            writer.Write((uint)contents.Length);
            writer.Write((uint)type);
            writer.Write(new byte[] { (byte)'C', (byte)'O', (byte)'R', (byte)'E', 0, 0, 0, 0 });
            writer.Write(contents);
            while (writer.BaseStream.Position % 4 != 0)
                writer.Write((byte)0);
        }

        private static MemoryStream CreateMachCore(Architecture architecture, uint floatingPointFlavor = Native.x86_FLOAT_STATE64)
        {
            bool x64 = architecture == Architecture.X64;
            byte[] general = new byte[x64 ? 168 : 272];
            byte[] floating = new byte[x64 ? floatingPointFlavor switch { Native.x86_AVX_STATE64 => 844, Native.x86_AVX512_STATE64 => 2444, _ => 524 } : 528];
            byte[] debug = new byte[x64 ? 64 : 520];
            Write(general, 0, RegisterValue + 1);
            if (x64)
                Write(general, 48, RegisterValue + 3);
            Write(floating, x64 ? 264 : 0, RegisterValue);
            Write(debug, 0, RegisterValue + 2);
            uint commandSize = (uint)(8 + 3 * 8 + general.Length + floating.Length + debug.Length);
            MemoryStream stream = new();
            using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(0xfeedfacfu);
                writer.Write(x64 ? 0x01000007u : 0x0100000cu);
                writer.Write(0u);
                writer.Write(4u);
                writer.Write(1u);
                writer.Write(commandSize);
                writer.Write(0ul);
                writer.Write(4u); // LC_THREAD
                writer.Write(commandSize);
                // Deliberately put FP before GPRs to exercise order-independent flavor parsing.
                WriteState(writer, x64 ? floatingPointFlavor : Native.ARM_NEON_STATE64, floating);
                WriteState(writer, (uint)(x64 ? Native.x86_THREAD_STATE64 : Native.ARM_THREAD_STATE64), general);
                WriteState(writer, x64 ? Native.x86_DEBUG_STATE64 : Native.ARM_DEBUG_STATE64, debug);
            }
            stream.Position = 0;
            return stream;
        }

        private static void WriteState(BinaryWriter writer, uint flavor, byte[] state)
        {
            writer.Write(flavor);
            writer.Write((uint)state.Length / 4);
            writer.Write(state);
        }

        private static T Read<T>(byte[] bytes, int offset) where T : unmanaged => MemoryMarshal.Read<T>(bytes.AsSpan(offset));

        private static void Write<T>(byte[] bytes, int offset, T value) where T : unmanaged => MemoryMarshal.Write(bytes.AsSpan(offset), in value);
    }
}