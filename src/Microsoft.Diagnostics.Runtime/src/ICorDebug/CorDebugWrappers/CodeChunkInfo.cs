using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct _CodeChunkInfo
    {
        public ulong startAddr;
        public uint length;
    }
}