using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_READ_USER_MINIDUMP_STREAM
    {
        public UInt32 StreamType;
        public UInt32 Flags;
        public UInt64 Offset;
        public IntPtr Buffer;
        public UInt32 BufferSize;
        public UInt32 BufferUsed;
    }
}