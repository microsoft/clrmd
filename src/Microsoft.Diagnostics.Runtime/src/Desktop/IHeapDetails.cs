namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IHeapDetails
    {
        ulong FirstHeapSegment { get; }
        ulong FirstLargeHeapSegment { get; }
        ulong EphemeralSegment { get; }
        ulong EphemeralEnd { get; }
        ulong EphemeralAllocContextPtr { get; }
        ulong EphemeralAllocContextLimit { get; }

        ulong Gen0Start { get; }
        ulong Gen0Stop { get; }
        ulong Gen1Start { get; }
        ulong Gen1Stop { get; }
        ulong Gen2Start { get; }
        ulong Gen2Stop { get; }

        ulong FQAllObjectsStart { get; }
        ulong FQAllObjectsStop { get; }
        ulong FQRootsStart { get; }
        ulong FQRootsStop { get; }
    }
}