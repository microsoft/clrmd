using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class ClrGenerationData
    {
        public ulong StartSegment { get; }
        public ulong AllocationStart { get; }
        public ulong AllocationContextPointer { get; }
        public ulong AllocationContextLimit { get; }

        internal ClrGenerationData(in GenerationData generationData)
        {
            StartSegment = generationData.StartSegment;
            AllocationStart = generationData.AllocationStart;
            AllocationContextPointer = generationData.AllocationContextPointer;
            AllocationContextLimit = generationData.AllocationContextLimit;
        }
    }
}
