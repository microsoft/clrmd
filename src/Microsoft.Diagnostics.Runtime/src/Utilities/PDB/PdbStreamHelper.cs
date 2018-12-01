// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    internal class PdbStreamHelper
    {
        internal PdbStreamHelper(Stream reader, int pageSize)
        {
            PageSize = pageSize;
            Reader = reader;
        }

        internal void Seek(int page, int offset)
        {
            Reader.Seek(page * PageSize + offset, SeekOrigin.Begin);
        }

        internal void Read(byte[] bytes, int offset, int count)
        {
            Reader.Read(bytes, offset, count);
        }

        internal int PagesFromSize(int size)
        {
            return (size + PageSize - 1) / PageSize;
        }

        internal readonly int PageSize;
        internal readonly Stream Reader;
    }
}