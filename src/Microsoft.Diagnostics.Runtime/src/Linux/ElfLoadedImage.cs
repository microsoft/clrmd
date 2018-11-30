using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfLoadedImage
    {
        private readonly List<ELFFileTableEntryPointers> _fileTable = new List<ELFFileTableEntryPointers>(4);
        private long _end;

        public string Path { get; }
        public long BaseAddress { get; private set; }
        public long Size => _end - BaseAddress;

        public ElfLoadedImage(string path)
        {
            Path = path;
        }

        public void AddTableEntryPointers(ELFFileTableEntryPointers pointers)
        {
            _fileTable.Add(pointers);

            var start = pointers.Start.ToInt64();
            if (BaseAddress == 0 || start < BaseAddress)
                BaseAddress = start;

            var end = pointers.Stop.ToInt64();
            if (_end < end)
                _end = end;
        }

        public override string ToString()
        {
            return Path;
        }
    }
}