namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IFieldInfo
    {
        uint InstanceFields { get; }
        uint StaticFields { get; }
        uint ThreadStaticFields { get; }
        ulong FirstField { get; }
    }
}