namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal struct IMAGE_OPTIONAL_HEADER_SPECIFIC
    {
        public ulong SizeOfStackReserve;
        public ulong SizeOfStackCommit;
        public ulong SizeOfHeapReserve;
        public ulong SizeOfHeapCommit;
        public uint LoaderFlags;
        public uint NumberOfRvaAndSizes;
    }
}
