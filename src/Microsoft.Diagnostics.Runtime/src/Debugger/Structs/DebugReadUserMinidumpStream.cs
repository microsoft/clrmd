using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_READ_USER_MINIDUMP_STREAM
    {
        public uint StreamType;
        public uint Flags;
        public ulong Offset;
        public IntPtr Buffer;
        public uint BufferSize;
        public uint BufferUsed;
    }
}