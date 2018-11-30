namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    ///  Usually about 300-500 elements long.
    ///  This does not have the right layout to use MinidumpArray
    /// </summary>
    internal class MinidumpMemory64List
    {
        // Declaration of unmanaged structure is
        //   public ulong NumberOfMemoryRanges; // offset 0
        //   public RVA64 BaseRVA; // offset 8
        //   MINIDUMP_MEMORY_DESCRIPTOR64[]; // var-length embedded array
        public MinidumpMemory64List(DumpPointer streamPointer)
        {
            _streamPointer = streamPointer;
        }

        private DumpPointer _streamPointer;

        public ulong Count
        {
            get
            {
                var count = _streamPointer.ReadInt64();
                return (ulong)count;
            }
        }
        public RVA64 BaseRva
        {
            get
            {
                var rva = _streamPointer.PtrToStructure<RVA64>(8);
                return rva;
            }
        }

        public MINIDUMP_MEMORY_DESCRIPTOR64 GetElement(uint idx)
        {
            // Embededded array starts at offset 16.
            var offset = 16 + idx * MINIDUMP_MEMORY_DESCRIPTOR64.SizeOf;
            return _streamPointer.PtrToStructure<MINIDUMP_MEMORY_DESCRIPTOR64>(offset);
        }
    }
}