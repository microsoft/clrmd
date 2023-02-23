namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A region of native memory allocated by CLR.
    /// </summary>
    public readonly struct ClrNativeHeapInfo
    {
        /// <summary>
        /// The base address of this heap.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// The size of this heap.  Note that Size may be null when we do not know the size of
        /// the heap.
        /// </summary>
        public ulong? Size { get; }

        /// <summary>
        /// The kind of heap this is.
        /// </summary>
        public NativeHeapKind Kind { get; }

        /// <summary>
        /// Whether this particular region of the heap is the current block or if it's been already
        /// filled.
        /// </summary>
        public bool IsCurrentBlock { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClrNativeHeapInfo(ulong address, ulong? size, NativeHeapKind kind, bool current)
        {
            Address = address;
            Size = size;
            Kind = kind;
            IsCurrentBlock = current;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (Size is ulong size)
                return $"[{Address:x},{Address+size:x}] - {Kind}";

            return $"{Address:x} - {Kind}";
        }
    }
}
