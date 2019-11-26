using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_MODULE_PARAMETERS
    {
        public ulong Base;
        public uint Size;
        public uint TimeDateStamp;
        public uint Checksum;
        public uint Flags;
        public uint SymbolType;
        public uint ImageNameSize;
        public uint ModuleNameSize;
        public uint LoadedImageNameSize;
        public uint SymbolFileNameSize;
        public uint MappedImageNameSize;
        public fixed ulong Reserved[2];
    }
}

