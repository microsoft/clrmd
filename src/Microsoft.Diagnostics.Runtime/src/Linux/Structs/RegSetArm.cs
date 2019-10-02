// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct RegSetArm
    {
        public uint r0;
        public uint r1;
        public uint r2;
        public uint r3;
        public uint r4;
        public uint r5;
        public uint r6;
        public uint r7;
        public uint r8;
        public uint r9;
        public uint r10;
        public uint fp;
        public uint ip;
        public uint sp;
        public uint lr;
        public uint pc;
        public uint cpsr;
        public uint orig_r0;

        public unsafe bool CopyContext(uint contextFlags, uint contextSize, void* context)
        {
            if (contextSize < ArmContext.Size)
                return false;

            ArmContext* ctx = (ArmContext*)context;
            ctx->ContextFlags = ArmContext.ContextControl | ArmContext.ContextInteger;
            ctx->R0 = r0;
            ctx->R1 = r1;
            ctx->R2 = r2;
            ctx->R3 = r3;
            ctx->R4 = r4;
            ctx->R5 = r5;
            ctx->R6 = r6;
            ctx->R7 = r7;
            ctx->R8 = r8;
            ctx->R9 = r9;
            ctx->R10 = r10;
            ctx->R11 = fp;
            ctx->R12 = ip;
            ctx->Sp = sp;
            ctx->Lr = lr;
            ctx->Pc = pc;
            ctx->Cpsr = cpsr;

            return true;
        }
    }
}