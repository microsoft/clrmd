// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfLoadedImage
    {
        private readonly List<ElfFileTableEntryPointers> _fileTable = new List<ElfFileTableEntryPointers>(4);
        private long _end;

        public string Path { get; }
        public long BaseAddress { get; private set; }
        public long Size => _end - BaseAddress;

        public ElfLoadedImage(string path)
        {
            Path = path;
        }

        public void AddTableEntryPointers(ElfFileTableEntryPointers pointers)
        {
            _fileTable.Add(pointers);

            long start = pointers.Start.ToInt64();
            if (BaseAddress == 0 || start < BaseAddress)
                BaseAddress = start;

            long end = pointers.Stop.ToInt64();
            if (_end < end)
                _end = end;
        }

        public override string ToString()
        {
            return Path;
        }
    }
}