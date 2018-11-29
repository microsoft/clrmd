using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct WorkRequestData
    {
        public readonly ulong Function;
        public readonly ulong Context;
        public readonly ulong NextWorkRequest;
    }
}