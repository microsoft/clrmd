// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Buffers.Binary;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Reads a stress log chunk into a pooled byte buffer and exposes
    /// validated views of its prev/next pointers and message body. Construction
    /// fails (returns <see langword="false"/>) on truncated reads, mismatched
    /// signatures, or when the allocation budget is exhausted.
    /// </summary>
    internal struct ChunkReader
    {
        private byte[]? _buffer;
        private readonly StressLogLayout _layout;
        private readonly AllocationBudget _budget;

        public ulong Address { get; private set; }
        public ulong Prev { get; private set; }
        public ulong Next { get; private set; }

        public bool IsLoaded => _buffer is not null;

        public ChunkReader(StressLogLayout layout, AllocationBudget budget)
        {
            _layout = layout;
            _budget = budget;
            _buffer = null;
            Address = 0;
            Prev = 0;
            Next = 0;
        }

        /// <summary>
        /// Read and validate the chunk located at <paramref name="address"/>.
        /// On success the chunk's body is accessible via <see cref="Buffer"/>.
        /// </summary>
        public bool TryLoad(IDataReader reader, ulong address)
        {
            // Reject addresses whose chunk range cannot be represented in
            // the target's address space. Without this guard the
            // BufferStart/BufferEnd accessors and the message-address
            // arithmetic in the iterator can wrap.
            if (!_layout.ChunkFits(address))
                return false;

            Address = address;
            if (_buffer is null)
            {
                if (!_budget.TryReserve(_layout.ChunkTotalSize))
                    return false;

                _buffer = ArrayPool<byte>.Shared.Rent(_layout.ChunkTotalSize);
            }

            Span<byte> dst = _buffer.AsSpan(0, _layout.ChunkTotalSize);
            int got = reader.Read(address, dst);
            if (got < _layout.ChunkTotalSize)
            {
                Address = 0;
                Prev = 0;
                Next = 0;
                return false;
            }

            uint sig1 = BinaryPrimitives.ReadUInt32LittleEndian(dst.Slice(_layout.ChunkSig1Offset));
            uint sig2 = BinaryPrimitives.ReadUInt32LittleEndian(dst.Slice(_layout.ChunkSig2Offset));
            if (sig1 != StressLogConstants.ChunkSignature || sig2 != StressLogConstants.ChunkSignature)
            {
                Prev = 0;
                Next = 0;
                return false;
            }

            Prev = _layout.ReadTargetPointer(dst, _layout.ChunkPrevOffset);
            Next = _layout.ReadTargetPointer(dst, _layout.ChunkNextOffset);
            return true;
        }

        /// <summary>The chunk's message buffer.</summary>
        public ReadOnlySpan<byte> Buffer
            => _buffer is null
                ? default
                : _buffer.AsSpan(_layout.ChunkBufOffset, _layout.ChunkBufferSize);

        /// <summary>Translate a target message address into an offset within <see cref="Buffer"/>.</summary>
        public bool TryAddressToOffset(ulong address, out int offset)
        {
            offset = -1;

            // Reject chunk addresses whose buffer range cannot be represented
            // in the target's address space. A malformed dump can put Address
            // near MaxAddress; the additions below would otherwise wrap and
            // let an out-of-range address pass the in-range check.
            if (!_layout.ChunkFits(Address))
                return false;

            ulong start = Address + (ulong)_layout.ChunkBufOffset;
            ulong end = start + (ulong)_layout.ChunkBufferSize;
            if (address < start || address >= end)
                return false;

            offset = (int)(address - start);
            return true;
        }

        public ulong BufferStartAddress => Address + (ulong)_layout.ChunkBufOffset;
        public ulong BufferEndAddress => BufferStartAddress + (ulong)_layout.ChunkBufferSize;

        public void Release()
        {
            if (_buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _budget.Release(_layout.ChunkTotalSize);
                _buffer = null;
            }
        }
    }
}
