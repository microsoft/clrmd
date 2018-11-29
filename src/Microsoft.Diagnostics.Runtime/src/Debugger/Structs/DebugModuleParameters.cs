using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_MODULE_PARAMETERS
    {
        public UInt64 Base;
        public UInt32 Size;
        public UInt32 TimeDateStamp;
        public UInt32 Checksum;
        public DEBUG_MODULE Flags;
        public DEBUG_SYMTYPE SymbolType;
        public UInt32 ImageNameSize;
        public UInt32 ModuleNameSize;
        public UInt32 LoadedImageNameSize;
        public UInt32 SymbolFileNameSize;
        public UInt32 MappedImageNameSize;
        public fixed UInt64 Reserved[2];
    }
}