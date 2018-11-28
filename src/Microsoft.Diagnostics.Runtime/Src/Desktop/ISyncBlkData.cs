namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface ISyncBlkData
    {
        bool Free { get; }
        ulong Address { get; }
        ulong Object { get; }
        ulong OwningThread { get; }
        bool MonitorHeld { get; }
        uint Recursion { get; }
        uint TotalCount { get; }
    }
}