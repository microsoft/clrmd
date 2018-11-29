using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_HEAPINFO
    {
        public uint areGCStructuresValid;  // TRUE if it's ok to walk the heap, FALSE otherwise.
        public uint pointerSize;           // The size of pointers on the target architecture in bytes.
        public uint numHeaps;              // The number of logical GC heaps in the process.
        public uint concurrent;            // Is the GC concurrent?
        public CorDebugGCType gcType;      // Workstation or Server?
    }
}