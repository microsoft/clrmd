namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IGCInfo
    {
        bool ServerMode { get; }
        int HeapCount { get; }
        int MaxGeneration { get; }
        bool GCStructuresValid { get; }
    }
}