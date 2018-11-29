namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Represents a Portable Executable (PE) Data directory.  This is just a well known optional 'Blob' of memory (has a starting point and size)
    /// </summary>
    public struct IMAGE_DATA_DIRECTORY
    {
        /// <summary>
        /// The start of the data blob when the file is mapped into memory
        /// </summary>
        public int VirtualAddress;
        /// <summary>
        /// The length of the data blob.  
        /// </summary>
        public int Size;
    }
}