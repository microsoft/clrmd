using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IClrGCHeapHelpers
    {
        int GenerationTableSize { get; }
        GenerationData[]? TryGetGenerationData(ulong serverAddress);
        ClrDataAddress[]? TryGetFinalizationPointers(ulong serverAddress);
        ClrSegment? CreateSegment(ClrHeap clrHeap, ulong address, int generation, ClrGCHeap gcHeap);
        bool GetHeapData(ulong address, out HeapDetails data);
        bool GetHeapData(out HeapDetails data);
        ClrDataAddress[] GetHeaps(int count);
    }

    internal class GCHeapHelpers : IClrGCHeapHelpers
    {
        public IMemoryReader _memoryReader { get; }

        private readonly SOSDac _sos;
        private readonly SOSDac8? _sos8;

        public int GenerationTableSize => _sos8?.GenerationCount ?? 4;

        public uint SizeOfPlugAndGap => (uint)_memoryReader.PointerSize * 4;

        public GCHeapHelpers(SOSDac sos, SOSDac8? sos8, IMemoryReader reader)
        {
            _memoryReader = reader;
            _sos = sos;
            _sos8 = sos8;
        }

        public bool GetHeapData(out HeapDetails data) => _sos.GetWksHeapDetails(out data);

        public bool GetHeapData(ulong address, out HeapDetails data) => _sos.GetServerHeapDetails(address, out data);

        public ClrDataAddress[] GetHeaps(int count) => _sos.GetHeapList(count);

        public GenerationData[]? TryGetGenerationData(ulong serverAddress)
        {
            if (serverAddress == 0)
                return _sos8?.GetGenerationTable();
            else
                return _sos8?.GetGenerationTable(serverAddress);
        }

        public ClrDataAddress[]? TryGetFinalizationPointers(ulong serverAddress)
        {
            if (serverAddress == 0)
                return _sos8?.GetFinalizationFillPointers();
            else
                return _sos8?.GetFinalizationFillPointers(serverAddress);
        }

        public ClrSegment? CreateSegment(ClrHeap clrHeap, ulong address, int generation, ClrGCHeap gcHeap)
        {
            const nint heap_segment_flags_readonly = 1;

            if (!_sos.GetSegmentData(address, out SegmentData data))
                return null;

            bool ro = (data.Flags & heap_segment_flags_readonly) == heap_segment_flags_readonly;

            SegmentKind kind = SegmentKind.Generation2;
            if (ro)
            {
                kind = SegmentKind.Frozen;
            }
            else if (generation == 3)
            {
                kind = SegmentKind.Large;
            }
            else if (generation == 4)
            {
                kind = SegmentKind.Pinned;
            }
            else
            {
                // We are not a Frozen, Large, or Pinned segment/region:
                if (gcHeap.HasRegions)
                {
                    if (generation == 0)
                        kind = SegmentKind.Generation0;
                    else if (generation == 1)
                        kind = SegmentKind.Generation1;
                    else if (generation == 2)
                        kind = SegmentKind.Generation2;
                }
                else
                {
                    if (gcHeap.EphemeralHeapSegment == address)
                        kind = SegmentKind.Ephemeral;
                    else
                        kind = SegmentKind.Generation2;
                }
            }

            // The range of memory occupied by allocated objects
            MemoryRange allocated = new(data.Start, gcHeap.EphemeralHeapSegment == address ? gcHeap.Allocated : (ulong)data.Allocated);

            MemoryRange committed, gen0, gen1, gen2;
            if (gcHeap.HasRegions)
            {
                committed = new(allocated.Start - SizeOfPlugAndGap, data.Committed);
                gen0 = default;
                gen1 = default;
                gen2 = default;

                switch (generation)
                {
                    case 0:
                        gen0 = new(allocated.Start, allocated.End);
                        break;

                    case 1:
                        gen1 = new(allocated.Start, allocated.End);
                        break;

                    default:
                        gen2 = new(allocated.Start, allocated.End);
                        break;
                }
            }
            else
            {
                committed = new(allocated.Start, data.Committed);
                if (kind == SegmentKind.Ephemeral)
                {
                    gen0 = new(gcHeap.GenerationTable[0].AllocationStart, allocated.End);
                    gen1 = new(gcHeap.GenerationTable[1].AllocationStart, gen0.Start);
                    gen2 = new(allocated.Start, gen1.Start);
                }
                else
                {
                    gen0 = default;
                    gen1 = default;
                    gen2 = allocated;
                }
            }

            // The range of memory reserved
            MemoryRange reserved = new(committed.End, data.Reserved);

            return new ClrSegment(clrHeap, _memoryReader)
            {
                Address = data.Address,
                LogicalHeap = gcHeap,
                Kind = kind,
                ObjectRange = allocated,
                CommittedMemory = committed,
                ReservedMemory = reserved,
                Generation0 = gen0,
                Generation1 = gen1,
                Generation2 = gen2,
                Next = data.Next,
            };
        }
    }
}
