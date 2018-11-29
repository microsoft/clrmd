using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_HANDLE_DATA_BASIC
    {
        public UInt32 TypeNameSize;
        public UInt32 ObjectNameSize;
        public UInt32 Attributes;
        public UInt32 GrantedAccess;
        public UInt32 HandleCount;
        public UInt32 PointerCount;
    }
}