// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        /// The additional state info of this memory, if applicable.
        /// </summary>
        public ClrNativeHeapState State { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClrNativeHeapInfo(ulong address, ulong? size, NativeHeapKind kind, ClrNativeHeapState state)
        {
            Address = address;
            Size = size;
            Kind = kind;
            State = state;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (Size is ulong size)
                return $"[{Address:x},{Address + size:x}] - {Kind}";

            return $"{Address:x} - {Kind}";
        }
    }
}