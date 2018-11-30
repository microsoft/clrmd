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
        public ulong FS;
        public ulong GS;
        public ulong DS;
        public ulong ES;
        public ulong Reg25;
        public ulong Reg26;
    }
}