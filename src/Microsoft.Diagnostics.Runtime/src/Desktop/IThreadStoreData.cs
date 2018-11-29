namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IThreadStoreData
    {
        ulong Finalizer { get; }
        ulong FirstThread { get; }
        int Count { get; }
    }
}