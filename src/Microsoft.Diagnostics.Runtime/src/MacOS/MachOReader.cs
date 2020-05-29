// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime.MacOS
{
    internal readonly struct MachOReader
    {
        private readonly Stream _stream;

        internal MachOReader(Stream stream)
        {
            _stream = stream;
        }

        internal int Read(long position, Span<byte> buffer)
        {
            _stream.Seek(position, SeekOrigin.Begin);
            return _stream.Read(buffer);
        }

        internal unsafe T Read<T>()
            where T : unmanaged
        {
            int size = sizeof(T);
            T result;
            if (_stream.Read(new Span<byte>(&result, size)) != size)
            {
                throw new IOException();
            }

            return result;
        }
    }
}
