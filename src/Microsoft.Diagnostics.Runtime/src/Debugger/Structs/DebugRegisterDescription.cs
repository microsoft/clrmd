using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_REGISTER_DESCRIPTION
    {
        public DEBUG_VALUE_TYPE Type;
        public DEBUG_REGISTER Flags;
        public UInt64 SubregMaster;
        public UInt64 SubregLength;
        public UInt64 SubregMask;
        public UInt64 SubregShift;
        public UInt64 Reserved0;
    }
}