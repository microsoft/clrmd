using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct DomainLocalModuleData : IDomainLocalModuleData
    {
        public readonly ulong AppDomainAddress;
        public readonly ulong ModuleID;

        public readonly ulong ClassData;
        public readonly ulong DynamicClassTable;
        public readonly ulong GCStaticDataStart;
        public readonly ulong NonGCStaticDataStart;

        ulong IDomainLocalModuleData.AppDomainAddr => AppDomainAddress;
        ulong IDomainLocalModuleData.ModuleID => ModuleID;
        ulong IDomainLocalModuleData.ClassData => ClassData;
        ulong IDomainLocalModuleData.DynamicClassTable => DynamicClassTable;
        ulong IDomainLocalModuleData.GCStaticDataStart => GCStaticDataStart;
        ulong IDomainLocalModuleData.NonGCStaticDataStart => NonGCStaticDataStart;
    }
}