using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ElfFileTableEntryPointers
    {
        public IntPtr Start;
        public IntPtr Stop;
        public IntPtr PageOffset;
    }
}