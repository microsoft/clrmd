using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Describes a data stream within the minidump
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MINIDUMP_LOCATION_DESCRIPTOR
    {
        /// <summary>
        /// Size of the stream in bytes.
        /// </summary>
        public uint DataSize;

        /// <summary>
        /// Offset (in bytes) from the start of the minidump to the data stream.
        /// </summary>
        public RVA Rva;

        /// <summary>
        /// True iff the data is missing.
        /// </summary>
        public bool IsNull => DataSize == 0 || Rva.IsNull;
    }
}