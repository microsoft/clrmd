using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RejitData
    {
        private readonly ulong RejitID;
        private readonly uint Flags;
        private readonly ulong NativeCodeAddr;
    }
}