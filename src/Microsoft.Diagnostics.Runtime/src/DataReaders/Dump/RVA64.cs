using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// RVAs are offsets into the minidump.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RVA64
    {
        public ulong Value;
    }
}