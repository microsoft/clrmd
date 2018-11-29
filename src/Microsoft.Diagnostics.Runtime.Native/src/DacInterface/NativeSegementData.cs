namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public readonly struct NativeSegementData
    {
        public readonly ulong Address;
        public readonly ulong Allocated;
        public readonly ulong Committed;
        public readonly ulong Reserved;
        public readonly ulong Used;
        public readonly ulong Mem;
        public readonly ulong Next;
        public readonly ulong Heap;
        public readonly ulong HighAllocMark;
        public readonly uint IsReadOnly;
    }
}