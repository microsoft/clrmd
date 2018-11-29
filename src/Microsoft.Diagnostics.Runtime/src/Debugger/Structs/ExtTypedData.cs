using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct _EXT_TYPED_DATA
    {
        public _EXT_TDOP Operation;
        public UInt32 Flags;
        public _DEBUG_TYPED_DATA InData;
        public _DEBUG_TYPED_DATA OutData;
        public UInt32 InStrIndex;
        public UInt32 In32;
        public UInt32 Out32;
        public UInt64 In64;
        public UInt64 Out64;
        public UInt32 StrBufferIndex;
        public UInt32 StrBufferChars;
        public UInt32 StrCharsNeeded;
        public UInt32 DataBufferIndex;
        public UInt32 DataBufferBytes;
        public UInt32 DataBytesNeeded;
        public UInt32 Status;
        public fixed UInt64 Reserved[8];
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public class EXT_TYPED_DATA
    {
        public _EXT_TDOP Operation;
        public UInt32 Flags;
        public _DEBUG_TYPED_DATA InData;
        public _DEBUG_TYPED_DATA OutData;
        public UInt32 InStrIndex;
        public UInt32 In32;
        public UInt32 Out32;
        public UInt64 In64;
        public UInt64 Out64;
        public UInt32 StrBufferIndex;
        public UInt32 StrBufferChars;
        public UInt32 StrCharsNeeded;
        public UInt32 DataBufferIndex;
        public UInt32 DataBufferBytes;
        public UInt32 DataBytesNeeded;
        public UInt32 Status;
    }
}