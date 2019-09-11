// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct RegSetArm64
    {
        public unsafe fixed ulong regs[31];
        public ulong sp;
        public ulong pc;
        public ulong pstate;

        public unsafe bool CopyContext(uint contextFlags, uint contextSize, void* context)
        {
            if (contextSize < Arm64Context.Size)
                return false;

            Arm64Context* ctx = (Arm64Context*)context;
            ctx->ContextFlags = Arm64Context.ContextControl | Arm64Context.ContextInteger;
            ctx->Cpsr = (uint)pstate;

            ctx->X0 = regs[0];
            ctx->X1 = regs[1];
            ctx->X2 = regs[2];
            ctx->X3 = regs[3];
            ctx->X4 = regs[4];
            ctx->X5 = regs[5];
            ctx->X6 = regs[6];
            ctx->X7 = regs[7];
            ctx->X8 = regs[8];
            ctx->X9 = regs[9];
            ctx->X10 = regs[10];
            ctx->X11 = regs[11];
            ctx->X12 = regs[12];
            ctx->X13 = regs[13];
            ctx->X14 = regs[14];
            ctx->X15 = regs[15];
            ctx->X16 = regs[16];
            ctx->X17 = regs[17];
            ctx->X18 = regs[18];
            ctx->X19 = regs[19];
            ctx->X20 = regs[20];
            ctx->X21 = regs[21];
            ctx->X22 = regs[22];
            ctx->X23 = regs[23];
            ctx->X24 = regs[24];
            ctx->X25 = regs[25];
            ctx->X26 = regs[26];
            ctx->X27 = regs[27];
            ctx->X28 = regs[28];

            ctx->Fp = regs[29];
            ctx->Lr = regs[30];
            ctx->Sp = sp;
            ctx->Pc = pc;

            return true;
        }
    }
}