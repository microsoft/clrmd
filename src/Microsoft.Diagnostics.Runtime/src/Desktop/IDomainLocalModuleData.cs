namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IDomainLocalModuleData
    {
        ulong AppDomainAddr { get; }
        ulong ModuleID { get; }

        ulong ClassData { get; }
        ulong DynamicClassTable { get; }
        ulong GCStaticDataStart { get; }
        ulong NonGCStaticDataStart { get; }
    }
}