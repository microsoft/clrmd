using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ElfFileTableHeader
    {
        public IntPtr EntryCount;
        public IntPtr PageSize;
    }
}