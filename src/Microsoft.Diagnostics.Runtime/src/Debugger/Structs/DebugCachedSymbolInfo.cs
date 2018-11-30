using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_CACHED_SYMBOL_INFO
    {
        public ulong ModBase;
        public ulong Arg1;
        public ulong Arg2;
        public uint Id;
        public uint Arg3;
    }
}