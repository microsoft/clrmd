// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    internal class DataStream
    {
        internal DataStream()
        {
        }

        internal DataStream(int contentSize, BitAccess bits, int count)
        {
            _contentSize = contentSize;
            if (count > 0)
            {
                _pages = new int[count];
                bits.ReadInt32(_pages);
            }
        }

        internal void Read(PdbStreamHelper reader, BitAccess bits)
        {
            bits.MinCapacity(_contentSize);
            Read(reader, 0, bits.Buffer, 0, _contentSize);
        }

        internal void Read(
            PdbStreamHelper reader,
            int position,
            byte[] bytes,
            int offset,
            int data)
        {
            if (position + data > _contentSize)
            {
                throw new PdbException(
                    "DataStream can't read off end of stream. " +
                    "(pos={0},siz={1})",
                    position,
                    data);
            }

            if (position == _contentSize)
            {
                return;
            }

            int left = data;
            int page = position / reader.PageSize;
            int rema = position % reader.PageSize;

            // First get remained of first page.
            if (rema != 0)
            {
                int todo = reader.PageSize - rema;
                if (todo > left)
                {
                    todo = left;
                }

                reader.Seek(_pages[page], rema);
                reader.Read(bytes, offset, todo);

                offset += todo;
                left -= todo;
                page++;
            }

            // Now get the remaining pages.
            while (left > 0)
            {
                int todo = reader.PageSize;
                if (todo > left)
                {
                    todo = left;
                }

                reader.Seek(_pages[page], 0);
                reader.Read(bytes, offset, todo);

                offset += todo;
                left -= todo;
                page++;
            }
        }

        internal int Length => _contentSize;

        internal int _contentSize;
        internal int[] _pages;
    }
}