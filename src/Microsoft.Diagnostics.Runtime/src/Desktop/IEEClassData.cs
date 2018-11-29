namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IEEClassData
    {
        ulong MethodTable { get; }
        ulong Module { get; }
    }
}