namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IAppDomainStoreData
    {
        ulong SharedDomain { get; }
        ulong SystemDomain { get; }
        int Count { get; }
    }
}