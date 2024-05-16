// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// RISC-V-specific thread context.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct RiscV64Context
    {
        public const uint Context = 0x01000000;
        public const uint ContextControl = Context | 0x1;
        public const uint ContextInteger = Context | 0x2;
        public const uint ContextFloatingPoint = Context | 0x4;
        public const uint ContextDebugRegisters = Context | 0x8;

        public static int Size => 0x220;

        // Control flags

        [FieldOffset(0x0)]
        public uint ContextFlags;

        #region General and control registers

        [Register(RegisterType.General)]
        [FieldOffset(0x8)]
        public ulong R0;

        [Register(RegisterType.General)]
        [FieldOffset(0x10)]
        public ulong Ra;

        [Register(RegisterType.Control | RegisterType.StackPointer)]
        [FieldOffset(0x18)]
        public ulong Sp;

        [Register(RegisterType.General)]
        [FieldOffset(0x20)]
        public ulong Gp;

        [Register(RegisterType.General)]
        [FieldOffset(0x28)]
        public ulong Tp;

        [Register(RegisterType.General)]
        [FieldOffset(0x30)]
        public ulong T0;

        [Register(RegisterType.General)]
        [FieldOffset(0x38)]
        public ulong T1;

        [Register(RegisterType.General)]
        [FieldOffset(0x40)]
        public ulong T2;

        [Register(RegisterType.Control | RegisterType.FramePointer)]
        [FieldOffset(0x48)]
        public ulong Fp;

        [Register(RegisterType.General)]
        [FieldOffset(0x50)]
        public ulong S1;

        [Register(RegisterType.General)]
        [FieldOffset(0x58)]
        public ulong A0;

        [Register(RegisterType.General)]
        [FieldOffset(0x60)]
        public ulong A1;

        [Register(RegisterType.General)]
        [FieldOffset(0x68)]
        public ulong A2;

        [Register(RegisterType.General)]
        [FieldOffset(0x70)]
        public ulong A3;

        [Register(RegisterType.General)]
        [FieldOffset(0x78)]
        public ulong A4;

        [Register(RegisterType.General)]
        [FieldOffset(0x80)]
        public ulong A5;

        [Register(RegisterType.General)]
        [FieldOffset(0x88)]
        public ulong A6;

        [Register(RegisterType.General)]
        [FieldOffset(0x90)]
        public ulong A7;

        [Register(RegisterType.General)]
        [FieldOffset(0x98)]
        public ulong S2;

        [Register(RegisterType.General)]
        [FieldOffset(0xa0)]
        public ulong S3;

        [Register(RegisterType.General)]
        [FieldOffset(0xa8)]
        public ulong S4;

        [Register(RegisterType.General)]
        [FieldOffset(0xb0)]
        public ulong S5;

        [Register(RegisterType.General)]
        [FieldOffset(0xb8)]
        public ulong S6;

        [Register(RegisterType.General)]
        [FieldOffset(0xc0)]
        public ulong S7;

        [Register(RegisterType.General)]
        [FieldOffset(0xc8)]
        public ulong S8;

        [Register(RegisterType.General)]
        [FieldOffset(0xd0)]
        public ulong S9;

        [Register(RegisterType.General)]
        [FieldOffset(0xd8)]
        public ulong S10;

        [Register(RegisterType.General)]
        [FieldOffset(0xe0)]
        public ulong S11;

        [Register(RegisterType.General)]
        [FieldOffset(0xe8)]
        public ulong T3;

        [Register(RegisterType.General)]
        [FieldOffset(0xf0)]
        public ulong T4;

        [Register(RegisterType.General)]
        [FieldOffset(0xf8)]
        public ulong T5;

        [Register(RegisterType.General)]
        [FieldOffset(0x100)]
        public ulong T6;

        [Register(RegisterType.Control | RegisterType.ProgramCounter)]
        [FieldOffset(0x108)]
        public ulong Pc;

        #endregion

        #region Floating Point

        [Register(RegisterType.FloatingPoint)]
        [FieldOffset(0x110)]
        public unsafe fixed ulong F[32];

        [Register(RegisterType.FloatingPoint)]
        [FieldOffset(0x210)]
        public uint Fcsr;

        #endregion
    }
}