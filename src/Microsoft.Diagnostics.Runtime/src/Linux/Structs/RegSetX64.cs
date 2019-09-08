// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct RegSetX64
    {
        public ulong R15;
        public ulong R14;
        public ulong R13;
        public ulong R12;
        public ulong Rbp;
        public ulong Rbx;
        public ulong R11;
        public ulong R10;
        public ulong R8;
        public ulong R9;
        public ulong Rax;
        public ulong Rcx;
        public ulong Rdx;
        public ulong Rsi;
        public ulong Rdi;
        public ulong OrigRax;
        public ulong Rip;
        public ulong CS;
        public ulong EFlags;
        public ulong Rsp;
        public ulong SS;
        public ulong FSBase;
        public ulong GSBase;
        public ulong DS;
        public ulong ES;
        public ulong FS;
        public ulong GS;

        public unsafe bool CopyContext(uint contextFlags, uint contextSize, void* context)
        {
            if (contextSize < AMD64Context.Size)
                return false;

            AMD64Context* ctx = (AMD64Context*)context;
            ctx->ContextFlags = AMD64Context.ContextControl | AMD64Context.ContextInteger | AMD64Context.ContextSegments;
            ctx->R15 = R15;
            ctx->R14 = R14;
            ctx->R13 = R13;
            ctx->R12 = R12;
            ctx->Rbp = Rbp;
            ctx->Rbx = Rbx;
            ctx->R11 = R11;
            ctx->R10 = R10;
            ctx->R9 = R9;
            ctx->R8 = R8;
            ctx->Rax = Rax;
            ctx->Rcx = Rcx;
            ctx->Rdx = Rdx;
            ctx->Rsi = Rsi;
            ctx->Rdi = Rdi;
            ctx->Rip = Rip;
            ctx->Rsp = Rsp;
            ctx->Cs = (ushort)CS;
            ctx->Ds = (ushort)DS;
            ctx->Ss = (ushort)SS;
            ctx->Fs = (ushort)FS;
            ctx->Gs = (ushort)GS;

            return true;
        }
    }
}