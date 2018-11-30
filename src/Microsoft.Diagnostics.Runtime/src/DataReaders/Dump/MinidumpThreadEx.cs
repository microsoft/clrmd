using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class MINIDUMP_THREAD_EX : MINIDUMP_THREAD
    {
        public override bool HasBackingStore()
        {
            return true;
        }

        public override MINIDUMP_MEMORY_DESCRIPTOR BackingStore { get; set; }
    }
}