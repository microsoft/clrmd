// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class ThreadContext
    {
        // These are CONTEXT register groups, not RegisterAttribute classifications.
        private static readonly (uint Flag, int Offset, int Size)[] s_x64 =
        {
            (AMD64Context.ContextControl, 0x38, 2), (AMD64Context.ContextControl, 0x42, 6),
            (AMD64Context.ContextControl, 0x98, 8), (AMD64Context.ContextControl, 0xf8, 8),
            (AMD64Context.ContextInteger, 0x78, 0x20), (AMD64Context.ContextInteger, 0xa0, 0x58),
            (AMD64Context.ContextSegments, 0x3a, 8),
            (AMD64Context.ContextFloatingPoint, 0x34, 4), (AMD64Context.ContextFloatingPoint, 0x100, 0x200),
            (AMD64Context.ContextDebugRegisters, 0x48, 0x30), (AMD64Context.ContextDebugRegisters, 0x4a8, 0x28)
        };
        private static readonly (uint Flag, int Offset, int Size)[] s_x86 =
        {
            (X86Context.ContextControl, 0xb4, 0x18), (X86Context.ContextInteger, 0x9c, 0x18),
            (X86Context.ContextSegments, 0x8c, 0x10), (X86Context.ContextFloatingPoint, 0x1c, 0x70),
            (X86Context.ContextDebugRegisters, 4, 0x18), (X86Context.ContextExtendedRegisters, 0xcc, 0x200)
        };
        private static readonly (uint Flag, int Offset, int Size)[] s_arm =
        {
            (ArmContext.ContextControl, 0x38, 0x10), (ArmContext.ContextInteger, 4, 0x34),
            (ArmContext.ContextFloatingPoint, 0x48, 4), (ArmContext.ContextFloatingPoint, 0x50, 0x100),
            (ArmContext.ContextDebugRegisters, 0x150, 0x48)
        };
        private static readonly (uint Flag, int Offset, int Size)[] s_arm64 =
        {
            (Arm64Context.ContextControl, 4, 4), (Arm64Context.ContextControl, 0xf0, 0x20),
            (Arm64Context.ContextInteger, 8, 0x90), (Arm64Context.ContextInteger, 0xa0, 0x50),
            (Arm64Context.ContextX18, 0x98, 8),
            (Arm64Context.ContextFloatingPointLow, 0x110, 0x100), (Arm64Context.ContextFloatingPointLow, 0x310, 8),
            (Arm64Context.ContextFloatingPointHigh, 0x210, 0x100),
            (Arm64Context.ContextDebugRegisters, 0x318, 0x78)
        };

        public static uint ArchitectureFlag(Architecture architecture) => architecture switch
        {
            Architecture.X64 => AMD64Context.Context,
            Architecture.X86 => X86Context.Context,
            Architecture.Arm => ArmContext.Context,
            Architecture.Arm64 => Arm64Context.Context,
            (Architecture)9 => RiscV64Context.Context,
            (Architecture)6 => LoongArch64Context.Context,
            _ => 0
        };

        public static uint FloatingPointFlag(Architecture architecture) => architecture is Architecture.X64 or Architecture.X86
            ? AMD64Context.ContextFloatingPoint & ~AMD64Context.Context
            : Arm64Context.ContextFloatingPoint & ~Arm64Context.Context;

        public static uint GetFlags(Architecture architecture, ReadOnlySpan<byte> context)
        {
            int offset = architecture == Architecture.X64 ? 0x30 : 0;
            return context.Length >= offset + sizeof(uint) ? MemoryMarshal.Read<uint>(context.Slice(offset)) : 0;
        }

        public static void SetFlags(Architecture architecture, Span<byte> context, uint flags)
        {
            MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(context.Slice(architecture == Architecture.X64 ? 0x30 : 0))) = flags;
        }

        public static bool Copy(Architecture architecture, uint requestedFlags, ReadOnlySpan<byte> source, Span<byte> destination)
        {
            const uint Arm64FloatingPoint = Arm64Context.ContextFloatingPoint & 0xffff;
            const uint Arm64FloatingPointParts = (Arm64Context.ContextFloatingPointLow | Arm64Context.ContextFloatingPointHigh) & 0xffff;

            uint archFlag = ArchitectureFlag(architecture);
            if (archFlag == 0 || destination.Length < (architecture == Architecture.X64 ? 0x30 : 0) + sizeof(uint))
                return false;

            uint available = GetFlags(architecture, source);
            if ((available & 0x01ff0000) != archFlag)
                return false;

            uint requested = requestedFlags == 0 ? uint.MaxValue : requestedFlags;
            if (architecture == Architecture.Arm64)
            {
                // The full FP flag includes both independently requestable halves.
                if ((available & Arm64FloatingPoint) != 0)
                    available |= Arm64FloatingPointParts;
                if ((requested & Arm64FloatingPoint) != 0)
                    requested |= Arm64FloatingPointParts;
            }

            uint flags = available & requested & 0xffff;
            var ranges = architecture switch
            {
                Architecture.X64 => s_x64,
                Architecture.X86 => s_x86,
                Architecture.Arm => s_arm,
                Architecture.Arm64 => s_arm64,
                _ => throw new PlatformNotSupportedException()
            };

            uint supported = 0;
            foreach (var range in ranges)
            {
                supported |= range.Flag;
                if ((flags & range.Flag) != 0 && destination.Length < range.Offset + range.Size)
                    return false;
                if (source.Length < range.Offset + range.Size)
                    flags &= ~range.Flag;
            }

            flags &= supported;
            foreach (var range in ranges)
                if ((flags & range.Flag) != 0)
                    source.Slice(range.Offset, range.Size).CopyTo(destination.Slice(range.Offset, range.Size));

            if (architecture == Architecture.Arm64 && (flags & Arm64FloatingPointParts) == Arm64FloatingPointParts &&
                (requested & Arm64FloatingPoint) != 0)
                flags = (flags & ~Arm64FloatingPointParts) | Arm64FloatingPoint;

            SetFlags(architecture, destination, archFlag | flags | (available & 0xfe000000));
            return true;
        }
    }
}