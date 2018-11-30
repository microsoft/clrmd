using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr Address;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;

        public ulong BaseAddress => (ulong)Address;
        public ulong Size => (ulong)RegionSize;
    }
}