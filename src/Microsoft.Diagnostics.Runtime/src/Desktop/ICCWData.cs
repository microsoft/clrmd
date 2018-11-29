namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface ICCWData
    {
        ulong IUnknown { get; }
        ulong Object { get; }
        ulong Handle { get; }
        ulong CCWAddress { get; }
        int RefCount { get; }
        int JupiterRefCount { get; }
        int InterfaceCount { get; }
    }
}