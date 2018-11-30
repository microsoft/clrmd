// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime
{
    public class GCDesc
    {
        private static readonly int s_GCDescSize = IntPtr.Size * 2;

        private readonly byte[] _data;

        public GCDesc(byte[] data)
        {
            _data = data;
        }

        public void WalkObject(ulong addr, ulong size, Func<ulong, ulong> readPointer, Action<ulong, int> refCallback)
        {
            Debug.Assert(size >= (ulong)IntPtr.Size);

            var series = GetNumSeries();
            var highest = GetHighestSeries();
            var curr = highest;

            if (series > 0)
            {
                var lowest = GetLowestSeries();
                do
                {
                    var ptr = addr + GetSeriesOffset(curr);
                    var stop = (ulong)((long)ptr + GetSeriesSize(curr) + (long)size);

                    while (ptr < stop)
                    {
                        var ret = readPointer(ptr);
                        if (ret != 0)
                            refCallback(ret, (int)(ptr - addr));

                        ptr += (ulong)IntPtr.Size;
                    }

                    curr -= s_GCDescSize;
                } while (curr >= lowest);
            }
            else
            {
                var ptr = addr + GetSeriesOffset(curr);

                while (ptr < addr + size - (ulong)IntPtr.Size)
                {
                    for (var i = 0; i > series; i--)
                    {
                        var nptrs = GetPointers(curr, i);
                        var skip = GetSkip(curr, i);

                        var stop = ptr + (ulong)(nptrs * IntPtr.Size);
                        do
                        {
                            var ret = readPointer(ptr);
                            if (ret != 0)
                                refCallback(ret, (int)(ptr - addr));

                            ptr += (ulong)IntPtr.Size;
                        } while (ptr < stop);

                        ptr += skip;
                    }
                }
            }
        }

        private uint GetPointers(int curr, int i)
        {
            var offset = i * IntPtr.Size;
            if (IntPtr.Size == 4)
                return BitConverter.ToUInt16(_data, curr + offset);

            return BitConverter.ToUInt32(_data, curr + offset);
        }

        private uint GetSkip(int curr, int i)
        {
            var offset = i * IntPtr.Size + IntPtr.Size / 2;
            if (IntPtr.Size == 4)
                return BitConverter.ToUInt16(_data, curr + offset);

            return BitConverter.ToUInt32(_data, curr + offset);
        }

        private int GetSeriesSize(int curr)
        {
            if (IntPtr.Size == 4)
                return BitConverter.ToInt32(_data, curr);

            return (int)BitConverter.ToInt64(_data, curr);
        }

        private ulong GetSeriesOffset(int curr)
        {
            ulong offset;
            if (IntPtr.Size == 4)
                offset = BitConverter.ToUInt32(_data, curr + IntPtr.Size);
            else
                offset = BitConverter.ToUInt64(_data, curr + IntPtr.Size);

            return offset;
        }

        private int GetHighestSeries()
        {
            return _data.Length - IntPtr.Size * 3;
        }

        private int GetLowestSeries()
        {
            return _data.Length - ComputeSize(GetNumSeries());
        }

        private static int ComputeSize(int series)
        {
            return IntPtr.Size + series * IntPtr.Size * 2;
        }

        private int GetNumSeries()
        {
            if (IntPtr.Size == 4)
                return BitConverter.ToInt32(_data, _data.Length - IntPtr.Size);

            return (int)BitConverter.ToInt64(_data, _data.Length - IntPtr.Size);
        }
    }
}