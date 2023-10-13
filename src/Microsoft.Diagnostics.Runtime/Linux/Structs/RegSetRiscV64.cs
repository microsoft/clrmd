// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct RegSetRiscV64
    {
        public readonly ulong R0;
        public readonly ulong Ra;
        public readonly ulong Sp;
        public readonly ulong Gp;
        public readonly ulong Tp;
        public readonly ulong T0;
        public readonly ulong T1;
        public readonly ulong T2;
        public readonly ulong Fp;
        public readonly ulong S1;
        public readonly ulong A0;
        public readonly ulong A1;
        public readonly ulong A2;
        public readonly ulong A3;
        public readonly ulong A4;
        public readonly ulong A5;
        public readonly ulong A6;
        public readonly ulong A7;
        public readonly ulong S2;
        public readonly ulong S3;
        public readonly ulong S4;
        public readonly ulong S5;
        public readonly ulong S6;
        public readonly ulong S7;
        public readonly ulong S8;
        public readonly ulong S9;
        public readonly ulong S10;
        public readonly ulong S11;
        public readonly ulong T3;
        public readonly ulong T4;
        public readonly ulong T5;
        public readonly ulong T6;
        public readonly ulong Pc;

        public unsafe bool CopyContext(Span<byte> context)
        {
            if (context.Length < RiscV64Context.Size)
                return false;

            ref RiscV64Context contextRef = ref Unsafe.As<byte, RiscV64Context>(ref MemoryMarshal.GetReference(context));

            contextRef.ContextFlags = RiscV64Context.ContextControl | RiscV64Context.ContextInteger;
            contextRef.R0 = R0;
            contextRef.Ra = Ra;
            contextRef.Sp = Sp;
            contextRef.Gp = Gp;
            contextRef.Tp = Tp;
            contextRef.T0 = T0;
            contextRef.T1 = T1;
            contextRef.T2 = T2;
            contextRef.Fp = Fp;
            contextRef.S1 = S1;
            contextRef.A0 = A0;
            contextRef.A1 = A1;
            contextRef.A2 = A2;
            contextRef.A3 = A3;
            contextRef.A4 = A4;
            contextRef.A5 = A5;
            contextRef.A6 = A6;
            contextRef.A7 = A7;
            contextRef.S2 = S2;
            contextRef.S3 = S3;
            contextRef.S4 = S4;
            contextRef.S5 = S5;
            contextRef.S6 = S6;
            contextRef.S7 = S7;
            contextRef.S8 = S8;
            contextRef.S9 = S9;
            contextRef.S10 = S10;
            contextRef.S11 = S11;
            contextRef.T3 = T3;
            contextRef.T4 = T4;
            contextRef.T5 = T5;
            contextRef.T6 = T6;
            contextRef.Pc = Pc;

            return true;
        }
    }
}