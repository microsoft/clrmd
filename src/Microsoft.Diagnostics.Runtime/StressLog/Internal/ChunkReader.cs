// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Buffers.Binary;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Reads a 64-bit stress log chunk into a pooled byte buffer and exposes
    /// validated views of its prev/next pointers and message body. Construction
    /// fails (returns <see langword="false"/>) on truncated reads, mismatched
    /// signatures, or when the allocation budget is exhausted.
    /// </summary>
    internal struct ChunkReader
    {
        private byte[]? _buffer;
        private readonly AllocationBudget _budget;

        public ulong Address { get; private set; }
        public ulong Prev { get; private set; }
        public ulong Next { get; private set; }

        public bool IsLoaded => _buffer is not null;

        public ChunkReader(AllocationBudget budget)
        {
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
            Address = address;
            if (_buffer is null)
            {
                if (!_budget.TryReserve(StressLogLayout.Chunk_TotalSize))
                    return false;

                _buffer = ArrayPool<byte>.Shared.Rent(StressLogLayout.Chunk_TotalSize);
            }

            Span<byte> dst = _buffer.AsSpan(0, StressLogLayout.Chunk_TotalSize);
            int got = reader.Read(address, dst);
            if (got < StressLogLayout.Chunk_TotalSize)
            {
                Address = 0;
                Prev = 0;
                Next = 0;
                return false;
            }

            uint sig1 = BinaryPrimitives.ReadUInt32LittleEndian(dst.Slice(StressLogLayout.Chunk_DwSig1Offset));
            uint sig2 = BinaryPrimitives.ReadUInt32LittleEndian(dst.Slice(StressLogLayout.Chunk_DwSig2Offset));
            if (sig1 != StressLogConstants.ChunkSignature || sig2 != StressLogConstants.ChunkSignature)
            {
                Prev = 0;
                Next = 0;
                return false;
            }

            Prev = BinaryPrimitives.ReadUInt64LittleEndian(dst.Slice(StressLogLayout.Chunk_Prev));
            Next = BinaryPrimitives.ReadUInt64LittleEndian(dst.Slice(StressLogLayout.Chunk_Next));
            return true;
        }

        /// <summary>The chunk's message buffer, addresses 0 .. ChunkBufferSize64 - 1.</summary>
        public ReadOnlySpan<byte> Buffer
            => _buffer is null
                ? default
                : _buffer.AsSpan(StressLogLayout.Chunk_Buf, StressLogConstants.ChunkBufferSize64);

        /// <summary>Translate a target message address into an offset within <see cref="Buffer"/>.</summary>
        public bool TryAddressToOffset(ulong address, out int offset)
        {
            ulong start = Address + (ulong)StressLogLayout.Chunk_Buf;
            ulong end = start + (ulong)StressLogConstants.ChunkBufferSize64;
            if (address < start || address >= end)
            {
                offset = -1;
                return false;
            }

            offset = (int)(address - start);
            return true;
        }

        public ulong BufferStartAddress => Address + (ulong)StressLogLayout.Chunk_Buf;
        public ulong BufferEndAddress => BufferStartAddress + (ulong)StressLogConstants.ChunkBufferSize64;

        public void Release()
        {
            if (_buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _budget.Release(StressLogLayout.Chunk_TotalSize);
                _buffer = null;
            }
        }
    }
}
