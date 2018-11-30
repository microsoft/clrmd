// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    internal class MsfDirectory
    {
        internal MsfDirectory(PdbStreamHelper reader, PdbFileHeader head, BitAccess bits)
        {
            var pages = reader.PagesFromSize(head.DirectorySize);

            // 0..n in page of directory pages.
            bits.MinCapacity(head.DirectorySize);
            var directoryRootPages = head.DirectoryRoot.Length;
            var pagesPerPage = head.PageSize / 4;
            var pagesToGo = pages;
            for (var i = 0; i < directoryRootPages; i++)
            {
                var pagesInThisPage = pagesToGo <= pagesPerPage ? pagesToGo : pagesPerPage;
                reader.Seek(head.DirectoryRoot[i], 0);
                bits.Append(reader.Reader, pagesInThisPage * 4);
                pagesToGo -= pagesInThisPage;
            }

            bits.Position = 0;

            var stream = new DataStream(head.DirectorySize, bits, pages);
            bits.MinCapacity(head.DirectorySize);
            stream.Read(reader, bits);

            // 0..3 in directory pages
            int count;
            bits.ReadInt32(out count);

            // 4..n
            var sizes = new int[count];
            bits.ReadInt32(sizes);

            // n..m
            _streams = new DataStream[count];
            for (var i = 0; i < count; i++)
            {
                if (sizes[i] <= 0)
                {
                    _streams[i] = new DataStream();
                }
                else
                {
                    _streams[i] = new DataStream(
                        sizes[i],
                        bits,
                        reader.PagesFromSize(sizes[i]));
                }
            }
        }

        internal DataStream[] _streams;
    }
}