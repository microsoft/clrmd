// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Native = Microsoft.Diagnostics.Runtime.MacOS.MacOSProcessDataReader.Native;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal struct thread_state_t
    {
        public x86_thread_state64_t x64;

        public arm_thread_state64_t arm;

        public byte[]? FloatingPoint;
        public byte[]? Debug;

        public bool CopyContext(Architecture architecture, uint contextFlags, Span<byte> context)
        {
            bool copied = architecture switch
            {
                Architecture.X64 => x64.CopyContext(contextFlags, context),
                Architecture.Arm64 => arm.CopyContext(contextFlags, context),
                _ => throw new PlatformNotSupportedException()
            };
            if (!copied)
                return false;

            if (FloatingPoint != null)
                CopyState(architecture, architecture == Architecture.X64 ? Native.x86_FLOAT_STATE64 : Native.ARM_NEON_STATE64, FloatingPoint, contextFlags, context);
            if (Debug != null)
                CopyState(architecture, architecture == Architecture.X64 ? Native.x86_DEBUG_STATE64 : Native.ARM_DEBUG_STATE64, Debug, contextFlags, context);
            return true;
        }

        public static int StateSize(Architecture architecture, uint flavor) => (architecture, flavor) switch
        {
            (Architecture.X64, Native.x86_THREAD_STATE64) => 168,
            (Architecture.X64, Native.x86_FLOAT_STATE64 or Native.x86_AVX_STATE64 or Native.x86_AVX512_STATE64) => 524, // Shared FXSAVE prefix.
            (Architecture.X64, Native.x86_DEBUG_STATE64) => 64,
            (Architecture.Arm64, Native.ARM_THREAD_STATE64) => 272,
            (Architecture.Arm64, Native.ARM_NEON_STATE64) => 528,
            (Architecture.Arm64, Native.ARM_DEBUG_STATE64) => 520,
            _ => 0
        };

        public static bool CopyState(Architecture architecture, uint flavor, ReadOnlySpan<byte> state, uint contextFlags, Span<byte> context)
        {
            int size = StateSize(architecture, flavor);
            if (size == 0 || state.Length < size)
                return false;

            uint requested = contextFlags == 0 ? uint.MaxValue : contextFlags;
            uint flags;
            switch (architecture, flavor)
            {
                case (Architecture.X64, Native.x86_THREAD_STATE64):
                    return MemoryMarshal.Read<x86_thread_state64_t>(state).CopyContext(contextFlags, context);
                case (Architecture.Arm64, Native.ARM_THREAD_STATE64):
                    return MemoryMarshal.Read<arm_thread_state64_t>(state).CopyContext(contextFlags, context);
                case (Architecture.X64, Native.x86_FLOAT_STATE64 or Native.x86_AVX_STATE64 or Native.x86_AVX512_STATE64):
                    if ((requested & (AMD64Context.ContextFloatingPoint & ~AMD64Context.Context)) == 0)
                        return true;
                    state.Slice(8, 512).CopyTo(context.Slice(0x100));
                    state.Slice(32, 4).CopyTo(context.Slice(0x34));
                    flags = AMD64Context.ContextFloatingPoint;
                    break;
                case (Architecture.Arm64, Native.ARM_NEON_STATE64):
                    if ((requested & (Arm64Context.ContextFloatingPoint & ~Arm64Context.Context)) == 0)
                        return true;
                    state.Slice(0, 512).CopyTo(context.Slice(0x110));
                    state.Slice(512, 4).CopyTo(context.Slice(0x314));
                    state.Slice(516, 4).CopyTo(context.Slice(0x310));
                    flags = Arm64Context.ContextFloatingPoint;
                    break;
                case (Architecture.X64, Native.x86_DEBUG_STATE64):
                    if ((requested & (AMD64Context.ContextDebugRegisters & ~AMD64Context.Context)) == 0)
                        return true;
                    state.Slice(0, 32).CopyTo(context.Slice(0x48));
                    state.Slice(48, 16).CopyTo(context.Slice(0x68));
                    flags = AMD64Context.ContextDebugRegisters;
                    break;
                case (Architecture.Arm64, Native.ARM_DEBUG_STATE64):
                    if ((requested & (Arm64Context.ContextDebugRegisters & ~Arm64Context.Context)) == 0)
                        return true;
                    state.Slice(0, 64).CopyTo(context.Slice(0x338));
                    state.Slice(256, 16).CopyTo(context.Slice(0x380));
                    for (int i = 0; i < 8; i++)
                        state.Slice(128 + i * 8, 4).CopyTo(context.Slice(0x318 + i * 4));
                    for (int i = 0; i < 2; i++)
                        state.Slice(384 + i * 8, 4).CopyTo(context.Slice(0x378 + i * 4));
                    flags = Arm64Context.ContextDebugRegisters;
                    break;
                default:
                    return false;
            }

            ThreadContext.SetFlags(architecture, context, ThreadContext.GetFlags(architecture, context) | flags);
            return true;
        }
    }
}