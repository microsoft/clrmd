﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal class ElfStringTable
    {
        private readonly Reader _reader;

        public ElfStringTable(Reader reader, ulong address, ulong size)
        {
            _reader = new Reader(new RelativeAddressSpace(reader.DataSource, "StringTable", address, size));
        }

        public string GetStringAtIndex(uint index) => _reader.ReadNullTerminatedAscii(index);

        internal static ElfStringTable? Create(Reader reader, ulong stringTableVA, ulong stringTableSize)
        {
            if (stringTableVA == 0 || stringTableSize == 0)
                return null;

            try
            {
                return new ElfStringTable(reader, stringTableVA, stringTableSize);
            }
            catch (IOException)
            {
            }
            catch (InvalidDataException)
            {
            }

            return null;
        }
    }
}