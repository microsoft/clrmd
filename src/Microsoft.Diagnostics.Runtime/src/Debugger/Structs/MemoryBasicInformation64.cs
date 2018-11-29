using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION64
    {
        public UInt64 BaseAddress;
        public UInt64 AllocationBase;
        public PAGE AllocationProtect;
        public UInt32 __alignment1;
        public UInt64 RegionSize;
        public MEM State;
        public PAGE Protect;
        public MEM Type;
        public UInt32 __alignment2;
    }
}