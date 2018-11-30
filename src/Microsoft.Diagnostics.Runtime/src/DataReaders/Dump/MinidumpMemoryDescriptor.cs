using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Describes a range of memory in the target.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MINIDUMP_MEMORY_DESCRIPTOR
    {
        public const int SizeOf = 16;

        /// <summary>
        /// Starting Target address of the memory range.
        /// </summary>
        private readonly ulong _startofmemoryrange;
        public ulong StartOfMemoryRange => DumpNative.ZeroExtendAddress(_startofmemoryrange);

        /// <summary>
        /// Location in minidump containing the memory corresponding to StartOfMemoryRage
        /// </summary>
        public MINIDUMP_LOCATION_DESCRIPTOR Memory;
    }
}