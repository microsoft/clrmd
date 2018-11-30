using System;

namespace Microsoft.Diagnostics.Runtime
{
    internal class ParallelObjectSet : ObjectSet
    {
        public ParallelObjectSet(ClrHeap heap) : base(heap)
        {
        }

        public override bool Contains(ulong obj)
        {
            if (GetSegment(obj, out var seg))
            {
                var offset = GetOffset(obj, seg);

                lock (seg.Objects.SyncRoot)
                    return seg.Objects[offset];
            }

            return false;
        }

        public override bool Add(ulong obj)
        {
            if (GetSegment(obj, out var seg))
            {
                var offset = GetOffset(obj, seg);

                lock (seg.Objects.SyncRoot)
                {
                    if (seg.Objects[offset])
                    {
                        return false;
                    }

                    seg.Objects.Set(offset, true);
                    Count++;
                    return true;
                }
            }

            return false;
        }

        public override bool Remove(ulong obj)
        {
            if (GetSegment(obj, out var seg))
            {
                var offset = GetOffset(obj, seg);
                lock (seg.Objects.SyncRoot)
                {
                    if (seg.Objects[offset])
                    {
                        seg.Objects.Set(offset, false);
                        Count--;
                        return true;
                    }
                }
            }

            return false;
        }

        public override void Clear()
        {
            throw new InvalidOperationException();
        }
    }
}