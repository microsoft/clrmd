using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ElfSignalInfo
    {
        public uint Number;
        public uint Code;
        public uint Errno;
    }
}