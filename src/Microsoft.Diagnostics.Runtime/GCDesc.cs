// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    public readonly struct GCDesc
    {
        private readonly int _pointerSize;
        private readonly int _gcDescSize;

        private readonly byte[] _data;

        public bool IsEmpty => _data is null;

        public GCDesc(byte[] data, int pointerSize)
        {
            _data = data;
            _pointerSize = pointerSize;
            _gcDescSize = pointerSize * 2;
        }

        public IEnumerable<(ulong ReferencedObject, int Offset)> WalkObject(byte[] buffer, int size)
        {
            DebugOnly.Assert(size >= _pointerSize);

            int series = GetNumSeries();
            int highest = GetHighestSeries();
            int curr = highest;

            if (series > 0)
            {
                int lowest = GetLowestSeries();
                do
                {
                    long offset = GetSeriesOffset(curr);
                    long stop = offset + GetSeriesSize(curr) + size;

                    while (offset < stop)
                    {
                        ulong ret = new Span<byte>(buffer).AsPointer(_pointerSize, (int)offset);
                        if (ret != 0)
                            yield return (ret, (int)offset);

                        offset += _pointerSize;
                    }

                    curr -= _gcDescSize;
                } while (curr >= lowest);
            }
            else
            {
                long offset = GetSeriesOffset(curr);

                while (offset < size - _pointerSize)
                {
                    for (int i = 0; i > series; i--)
                    {
                        int nptrs = GetPointers(curr, i);
                        int skip = GetSkip(curr, i);

                        long stop = offset + (nptrs * _pointerSize);
                        do
                        {
                            ulong ret = new Span<byte>(buffer).AsPointer(_pointerSize, (int)offset);
                            if (ret != 0)
                                yield return (ret, (int)offset);

                            offset += _pointerSize;
                        } while (offset < stop);

                        offset += skip;
                    }
                }
            }
        }

        /// <summary>
        /// Span-based, allocation-free walker that mirrors <see cref="WalkObject(byte[], int)"/>
        /// but reads directly from a <see cref="ReadOnlySpan{T}"/> over caller-supplied
        /// (typically directly-mapped) memory. References and their offsets are written into
        /// the caller's <paramref name="refsOut"/> and <paramref name="offsetsOut"/> buffers
        /// in walk order. The two output buffers must be the same length.
        ///
        /// <para>
        /// This shape exists because <see cref="WalkObject(byte[], int)"/> is an iterator and
        /// C# disallows holding a <see cref="ReadOnlySpan{T}"/> across a <c>yield return</c>;
        /// callers consume the returned count and walk the output buffers themselves.
        /// </para>
        /// </summary>
        /// <returns>The number of references written. Equal to <c>refsOut.Length</c> when the
        /// output buffer was too small (caller should fall back to <see cref="WalkObject"/>).</returns>
        internal int WalkObjectIntoBuffer(ReadOnlySpan<byte> buffer, int size, Span<ulong> refsOut, Span<int> offsetsOut)
        {
            DebugOnly.Assert(size >= _pointerSize);
            DebugOnly.Assert(refsOut.Length == offsetsOut.Length);

            int count = 0;
            int max = refsOut.Length;

            int series = GetNumSeries();
            int highest = GetHighestSeries();
            int curr = highest;

            if (series > 0)
            {
                int lowest = GetLowestSeries();
                do
                {
                    long offset = GetSeriesOffset(curr);
                    long stop = offset + GetSeriesSize(curr) + size;

                    while (offset < stop)
                    {
                        ulong ret = ReadPointer(buffer, (int)offset);
                        if (ret != 0)
                        {
                            if (count >= max)
                                return count;
                            refsOut[count] = ret;
                            offsetsOut[count] = (int)offset;
                            count++;
                        }

                        offset += _pointerSize;
                    }

                    curr -= _gcDescSize;
                } while (curr >= lowest);
            }
            else
            {
                long offset = GetSeriesOffset(curr);

                while (offset < size - _pointerSize)
                {
                    for (int i = 0; i > series; i--)
                    {
                        int nptrs = GetPointers(curr, i);
                        int skip = GetSkip(curr, i);

                        long stop = offset + (nptrs * _pointerSize);
                        do
                        {
                            ulong ret = ReadPointer(buffer, (int)offset);
                            if (ret != 0)
                            {
                                if (count >= max)
                                    return count;
                                refsOut[count] = ret;
                                offsetsOut[count] = (int)offset;
                                count++;
                            }

                            offset += _pointerSize;
                        } while (offset < stop);

                        offset += skip;
                    }
                }
            }

            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ReadPointer(ReadOnlySpan<byte> span, int offset)
        {
            if (_pointerSize == 8)
            {
                DebugOnly.Assert(span.Length - offset >= sizeof(ulong));
                return Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef(in span[offset]));
            }
            else
            {
                DebugOnly.Assert(span.Length - offset >= sizeof(uint));
                return Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(in span[offset]));
            }
        }

        private int GetPointers(int curr, int i)
        {
            int offset = i * _pointerSize;
            if (_pointerSize == 4)
                return BitConverter.ToUInt16(_data, curr + offset);

            return BitConverter.ToInt32(_data, curr + offset);
        }

        private int GetSkip(int curr, int i)
        {
            int offset = i * _pointerSize + _pointerSize / 2;
            if (_pointerSize == 4)
                return BitConverter.ToInt16(_data, curr + offset);

            return BitConverter.ToInt32(_data, curr + offset);
        }

        private int GetSeriesSize(int curr)
        {
            if (_pointerSize == 4)
                return BitConverter.ToInt32(_data, curr);

            return (int)BitConverter.ToInt64(_data, curr);
        }

        private long GetSeriesOffset(int curr)
        {
            long offset;
            if (_pointerSize == 4)
                offset = BitConverter.ToUInt32(_data, curr + _pointerSize);
            else
                offset = BitConverter.ToInt64(_data, curr + _pointerSize);

            return offset;
        }

        private int GetHighestSeries()
        {
            return _data.Length - _pointerSize * 3;
        }

        private int GetLowestSeries()
        {
            return _data.Length - ComputeSize(GetNumSeries());
        }

        private int ComputeSize(int series)
        {
            return _pointerSize + series * _pointerSize * 2;
        }

        private int GetNumSeries()
        {
            if (_pointerSize == 4)
                return BitConverter.ToInt32(_data, _data.Length - _pointerSize);

            return (int)BitConverter.ToInt64(_data, _data.Length - _pointerSize);
        }
    }
}