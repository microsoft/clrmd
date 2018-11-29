namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface ISegmentData
    {
        ulong Address { get; }
        ulong Next { get; }
        ulong Start { get; }
        ulong End { get; }
        ulong Committed { get; }
        ulong Reserved { get; }
    }
}