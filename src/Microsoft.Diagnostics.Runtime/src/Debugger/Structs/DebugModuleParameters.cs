using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_MODULE_PARAMETERS
    {
        public ulong Base;
        public uint Size;
        public uint TimeDateStamp;
        public uint Checksum;
        public DEBUG_MODULE Flags;
        public DEBUG_SYMTYPE SymbolType;
        public uint ImageNameSize;
        public uint ModuleNameSize;
        public uint LoadedImageNameSize;
        public uint SymbolFileNameSize;
        public uint MappedImageNameSize;
        public fixed ulong Reserved[2];
    }
}