// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime
{
    internal class GCDesc
    {
        private static readonly int s_GCDescSize = IntPtr.Size * 2;

        #region Variables
        private byte[] _data;
        #endregion

        #region Functions
        public GCDesc(byte[] data)
        {
            _data = data;
        }

        public void WalkObject(ulong addr, ulong size, MemoryReader cache, Action<ulong, int> refCallback)
        {
            Debug.Assert(size >= (ulong)IntPtr.Size);

            int series = GetNumSeries();
            int highest = GetHighestSeries();
            int curr = highest;

            if (series > 0)
            {
                int lowest = GetLowestSeries();
                do
                {
                    ulong ptr = addr + GetSeriesOffset(curr);
                    ulong stop = (ulong)((long)ptr + (long)GetSeriesSize(curr) + (long)size);

                    while (ptr < stop)
                    {
                        if (cache.ReadPtr(ptr, out ulong ret) && ret != 0)
                            refCallback(ret, (int)(ptr - addr));

                        ptr += (ulong)IntPtr.Size;
                    }

                    curr -= s_GCDescSize;
                } while (curr >= lowest);
            }
            else
            {
                ulong ptr = addr + GetSeriesOffset(curr);

                while (ptr < (addr + size - (ulong)IntPtr.Size))
                {
                    for (int i = 0; i > series; i--)
                    {
                        uint nptrs = GetPointers(curr, i);
                        uint skip = GetSkip(curr, i);

                        ulong stop = ptr + (ulong)(nptrs * IntPtr.Size);
                        do
                        {
                            if (cache.ReadPtr(ptr, out ulong ret) && ret != 0)
                                refCallback(ret, (int)(ptr - addr));

                            ptr += (ulong)IntPtr.Size;
                        } while (ptr < stop);

                        ptr += skip;
                    }
                }
            }
        }
        #endregion

        #region Private Functions
        private uint GetPointers(int curr, int i)
        {
            int offset = i * IntPtr.Size;
            if (IntPtr.Size == 4)
                return BitConverter.ToUInt16(_data, curr + offset);
            else
                return BitConverter.ToUInt32(_data, curr + offset);
        }

        private uint GetSkip(int curr, int i)
        {
            int offset = i * IntPtr.Size + IntPtr.Size / 2;
            if (IntPtr.Size == 4)
                return BitConverter.ToUInt16(_data, curr + offset);
            else
                return BitConverter.ToUInt32(_data, curr + offset);
        }

        private int GetSeriesSize(int curr)
        {
            if (IntPtr.Size == 4)
                return (int)BitConverter.ToInt32(_data, curr);
            else
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

        static private int ComputeSize(int series)
        {
            return IntPtr.Size + series * IntPtr.Size * 2;
        }

        private int GetNumSeries()
        {
            if (IntPtr.Size == 4)
                return (int)BitConverter.ToInt32(_data, _data.Length - IntPtr.Size);
            else
                return (int)BitConverter.ToInt64(_data, _data.Length - IntPtr.Size);
        }
        #endregion
    }
}