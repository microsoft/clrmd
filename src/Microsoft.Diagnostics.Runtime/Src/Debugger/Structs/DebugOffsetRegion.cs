using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_OFFSET_REGION
    {
        private UInt64 _base;
        private UInt64 _size;
    }
}