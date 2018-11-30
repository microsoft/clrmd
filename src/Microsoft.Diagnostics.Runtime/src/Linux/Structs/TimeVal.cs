using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct TimeVal
    {
        public long Seconds;
        public long Milliseconds;
    }
}