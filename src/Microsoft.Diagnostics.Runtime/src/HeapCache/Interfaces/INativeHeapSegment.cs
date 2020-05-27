namespace DumpAnalyzer.Definitions.Interfaces.Native
{
    public interface INativeHeapSegment
    {
        ulong Start { get; }

        ulong End { get; }
    }
}
