// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

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