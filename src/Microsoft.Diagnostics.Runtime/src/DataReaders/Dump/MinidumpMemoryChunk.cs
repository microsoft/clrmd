using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal class MinidumpMemoryChunk : IComparable<MinidumpMemoryChunk>
    {
        public ulong Size;
        public ulong TargetStartAddress;
        // TargetEndAddress is the first byte beyond the end of this chunk.
        public ulong TargetEndAddress;
        public ulong RVA;

        public int CompareTo(MinidumpMemoryChunk other)
        {
            return TargetStartAddress.CompareTo(other.TargetStartAddress);
        }
    }
}