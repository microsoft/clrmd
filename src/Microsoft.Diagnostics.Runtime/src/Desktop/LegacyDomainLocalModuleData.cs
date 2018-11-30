using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct LegacyDomainLocalModuleData : IDomainLocalModuleData
    {
        private IntPtr _moduleID;
        
        public ulong AppDomainAddr { get; }
        public ulong ModuleID => (ulong)_moduleID.ToInt64();
        public ulong ClassData { get; }
        public ulong DynamicClassTable { get; }
        public ulong GCStaticDataStart { get; }
        public ulong NonGCStaticDataStart { get; }
    }
}