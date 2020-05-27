namespace DumpAnalyzer.Definitions.Interfaces.Native
{
    public interface INativeHeapSegment
    {
        ulong VirtualAddress { get; }

        ulong End { get; }
    }
}
