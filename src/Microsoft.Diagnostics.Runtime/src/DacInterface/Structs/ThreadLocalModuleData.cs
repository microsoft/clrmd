using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ThreadLocalModuleData
    {
        public readonly ulong ThreadAddress;
        public readonly ulong ModuleIndex;

        public readonly ulong ClassData;
        public readonly ulong DynamicClassTable;
        public readonly ulong GCStaticDataStart;
        public readonly ulong NonGCStaticDataStart;
    }
}