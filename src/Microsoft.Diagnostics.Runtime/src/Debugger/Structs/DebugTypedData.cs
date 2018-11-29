using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct _DEBUG_TYPED_DATA
    {
        public UInt64 ModBase;
        public UInt64 Offset;
        public UInt64 EngineHandle;
        public UInt64 Data;
        public UInt32 Size;
        public UInt32 Flags;
        public UInt32 TypeId;
        public UInt32 BaseTypeId;
        public UInt32 Tag;
        public UInt32 Register;
        public fixed UInt64 Internal[9];
    }
}