using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// RVAs are offsets into the minidump.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RVA
    {
        public uint Value;

        public bool IsNull => Value == 0;
    }
}