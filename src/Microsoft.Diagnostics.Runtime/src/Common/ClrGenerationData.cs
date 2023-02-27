using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime
{
    public class ClrGenerationData
    {
        public ulong StartSegment { get; }
        public ulong AllocationStart { get; }
        public ulong AllocationContextPointer { get; }
        public ulong AllocationContextLimit { get; }

        public ClrGenerationData(in GenerationData generationData)
        {
            StartSegment = generationData.StartSegment;
            AllocationStart = generationData.AllocationStart;
            AllocationContextPointer = generationData.AllocationContextPointer;
            AllocationContextLimit = generationData.AllocationContextLimit;
        }
    }
}
