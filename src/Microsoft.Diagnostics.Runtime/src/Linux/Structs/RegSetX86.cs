// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct RegSetX86
    {
        public readonly uint Ebx;
        public readonly uint Ecx;
        public readonly uint Edx;
        public readonly uint Esi;
        public readonly uint Edi;
        public readonly uint Ebp;
        public readonly uint Eax;
        public readonly uint Xds;
        public readonly uint Xes;
        public readonly uint Xfs;
        public readonly uint Xgs;
        public readonly uint OrigEax;
        public readonly uint Eip;
        public readonly uint Xcs;
        public readonly uint EFlags;
        public readonly uint Esp;
        public readonly uint Xss;

        public unsafe bool CopyContext(uint contextFlags, uint contextSize, void* context)
        {
            if (contextSize < X86Context.Size)
                return false;

            X86Context* ctx = (X86Context*)context;

            ctx->ContextFlags = X86Context.ContextControl | X86Context.ContextInteger;

            ctx->Ebp = Ebp;
            ctx->Eip = Eip;
            ctx->Ecx = Ecx;
            ctx->EFlags = EFlags;
            ctx->Esp = Esp;
            ctx->Ss = Xss;

            ctx->Edi = Edi;
            ctx->Esi = Esi;
            ctx->Ebx = Ebx;
            ctx->Edx = Edx;
            ctx->Ecx = Ecx;
            ctx->Eax = Eax;

            return true;
        }
    }
}
