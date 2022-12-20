using Microsoft.Diagnostics.Runtime;
using System.Text;
using static DbgEngExtension.MAddress;

namespace DbgEngExtension
{
    // Commands for helping locate native memory held onto by the GC heap.
    public class FindPointers : DbgEngCommand
    {
        internal const string GCRefsToCommand = "findgcrefsto";
        public FindPointers(nint pUnknown, bool redirectConsoleOutput = true) : base(pUnknown, redirectConsoleOutput)
        {
        }

        public FindPointers(IDisposable dbgeng, bool redirectConsoleOutput = false) : base(dbgeng, redirectConsoleOutput)
        {
        }

        public void FindGCRefsTo(string args)
        {
            string[] types = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (types.Length == 0)
                Console.WriteLine($"usage: !{GCRefsToCommand} [TYPES]");
            else
                PrintGCPointersToMemory(verbose: false, types);
        }

        private void PrintGCPointersToMemory(bool verbose, string[] memoryTypes)
        {
            MAddress maddressHelper = new(this);
            IEnumerable<AddressMemoryRange> rangeEnum = maddressHelper.EnumerateAddressSpace(tagClrMemoryRanges: true, includeReserveMemory: false, tagReserveMemoryHeuristically: false);
            rangeEnum = rangeEnum.Where(r => memoryTypes.Any(memType => r.Name.Equals(memType, StringComparison.OrdinalIgnoreCase)));
            rangeEnum = rangeEnum.OrderBy(r => r.Start);

            AddressMemoryRange[] ranges = rangeEnum.ToArray();

            if (ranges.Length == 0)
            {
                Console.WriteLine($"No matching memory ranges.");
                Console.WriteLine(" ");
                return;
            }

            Console.WriteLine("Walking GC heap to find pointers...");
            Dictionary<ClrSegment, List<GCObjectToRange>> segmentLists = new();


            var items = Runtimes
                            .SelectMany(r => r.Heap.Segments)
                            .SelectMany(Segment => maddressHelper
                                                    .EnumerateRegionPointers(Segment.ObjectRange.Start, (int)Segment.ObjectRange.Length, ranges)
                                                    .Select(regionPointer => (Segment, regionPointer.Address, regionPointer.MemoryRange)));

            foreach (var item in items)
            {
                if (!segmentLists.TryGetValue(item.Segment, out List<GCObjectToRange>? list))
                    list = segmentLists[item.Segment] = new();

                list.Add(new GCObjectToRange(item.Address, item.MemoryRange));
            }

            Console.WriteLine("Resolving object names...");
            foreach (string type in memoryTypes)
            {
                List<ulong> addressesNotInObjects = new();
                Dictionary<string, int> unknownObjCounts = new();
                Dictionary<ulong, KnownClrMemoryPointer> knownMemory = new();
                Dictionary<ulong, int> sizeHints = new();

                foreach (var segEntry in segmentLists)
                {
                    ClrSegment seg = segEntry.Key;
                    List<GCObjectToRange> pointers = segEntry.Value;
                    pointers.Sort((x, y) => x.SegmentPointer.CompareTo(y.SegmentPointer));

                    int index = 0;
                    foreach (ClrObject obj in seg.EnumerateObjects())
                    {
                        if (index >= pointers.Count)
                            break;

                        while (index < pointers.Count && pointers[index].SegmentPointer < obj.Address)
                        {
                            // If we "missed" the pointer then it's outside of an object range.
                            addressesNotInObjects.Add(pointers[index].SegmentPointer);

                            Console.WriteLine($"Skipping {pointers[index].SegmentPointer:x} lastObj={obj.Address:x}-{obj.Address + obj.Size:x} {obj.Type?.Name}");

                            index++;
                        }

                        if (index == pointers.Count)
                            break;

                        while (index < pointers.Count && obj.Address <= pointers[index].SegmentPointer && pointers[index].SegmentPointer < obj.Address + obj.Size)
                        {
                            string typeName = obj.Type?.Name ?? $"<unknown_type>";

                            if (obj.IsFree)
                            {
                                // This is free space, if we found a pointer here then it was likely just relocated and we'll mark it elsewhere
                            }
                            else if (pointers[index].NativeMemoryRange.Name != type)
                            {
                                // This entry is for a different memory type, we'll get it on another pass
                            }
                            else if (knownMemory.ContainsKey(obj))
                            {
                                // do nothing, we already marked this memory
                            }
                            else if (KnownClrMemoryPointer.ContainsKnownClrMemoryPointers(obj))
                            {
                                foreach (KnownClrMemoryPointer knownMem in KnownClrMemoryPointer.EnumerateKnownClrMemoryPointers(obj, sizeHints))
                                    knownMemory.Add(obj, knownMem);
                            }
                            else
                            {
                                if (typeName.Contains('>'))
                                    typeName = CollapseGenerics(typeName);

                                unknownObjCounts.TryGetValue(typeName, out int count);
                                unknownObjCounts[typeName] = count + 1;
                            }

                            index++;
                        }
                    }
                }

                Console.WriteLine(" ");
                if (knownMemory.Count == 0 && unknownObjCounts.Count == 0)
                {
                    Console.WriteLine($"No GC heap pointers to '{type}' regions.");
                }
                else
                {
                    Console.WriteLine($"GC heap pointers to '{type}':");
                    if (knownMemory.Count > 0)
                    {
                        Console.WriteLine($"    Known memory pointers:");

                        var knownParts = from known in knownMemory.Values
                                         group known by known.Pointer into g
                                         let Count = g.Count()
                                         let MaxSize = g.Max(k => GetSize(sizeHints, k))
                                         orderby MaxSize descending, Count descending
                                         select new
                                         {
                                             Pointer = g.Key,
                                             Entries = g.OrderBy(o => o.Object.Address),
                                             Count,
                                             MaxSize,
                                         };

                        foreach (var item in knownParts)
                        {
                            if (item.Count == 1)
                            {
                                Console.WriteLine($"        {item.Pointer:x} 0x{item.MaxSize:x4} bytes via {item.Entries.Single().Name}");
                            }
                            else
                            {
                                if (verbose)
                                {
                                    Console.WriteLine($"        {item.Pointer:x} 0x{item.MaxSize:x4} bytes:");
                                    foreach (var obj in item.Entries)
                                        Console.WriteLine($"            {obj.Object.Address:x} {obj.Name}");
                                }
                                else
                                {
                                    Console.WriteLine($"        {item.Pointer:x} 0x{item.MaxSize:x4} bytes: {GetUniqueNameString(item.Entries)} ({item.Count} objects)");
                                }
                            }
                        }

                        var ordered = from item in knownMemory.Values
                                      orderby item.Pointer ascending, item.Size descending
                                      select item;

                        int totalRegions = 0;
                        ulong totalBytes = 0;
                        ulong prevEnd = 0;

                        foreach (var item in ordered)
                        {
                            ulong size = GetSize(sizeHints, item);

                            // overlapped pointer
                            if (item.Pointer < prevEnd)
                            {
                                if (item.Pointer + size <= prevEnd)
                                    continue;

                                ulong diff = prevEnd - item.Pointer;
                                if (diff >= size)
                                    continue;

                                size -= diff;
                                prevEnd += size;
                            }
                            else
                            {
                                totalRegions++;
                                prevEnd = item.Pointer + size;
                            }

                            totalBytes += size;
                        }

                        Console.WriteLine(" ");
                        Console.WriteLine($"        {totalBytes:n0} total bytes ({totalBytes.ConvertToHumanReadable()}) in {totalRegions:n0} regions.");
                        Console.WriteLine(" ");
                    }


                    if (unknownObjCounts.Count > 0)
                    {
                        Console.WriteLine($"    Other memory pointers:");
                        var countParts = from kv in unknownObjCounts
                                         let Type = kv.Key
                                         let Count = kv.Value
                                         orderby Count descending
                                         select new
                                         {
                                             Type,
                                             Count
                                         };

                        foreach (var item in countParts)
                        {
                            Console.WriteLine($"      {item.Count,12:n0} {item.Type}");
                        }

                        Console.WriteLine(" ");
                    }
                }
            }
        }

        private static string GetUniqueNameString(IOrderedEnumerable<KnownClrMemoryPointer> entries)
        {
            HashSet<string> typeNames = new(entries.Select(e => e.Name));
            return string.Join(", ", typeNames.Order());
        }

        private static string CollapseGenerics(string typeName)
        {
            StringBuilder result = new(typeName.Length + 16);
            int nest = 0;
            for (int i = 0; i < typeName.Length; i++)
            {
                if (typeName[i] == '<')
                {
                    if (nest++ == 0)
                    {
                        if (i < typeName.Length - 1 && typeName[i + 1] == '>')
                            result.Append("<>");
                        else
                            result.Append("<...>");
                    }
                }
                else if (typeName[i] == '>')
                {
                    nest--;
                }
                else if (nest == 0)
                {
                    result.Append(typeName[i]);
                }
            }

            return result.ToString();
        }

        private static ulong GetSize(Dictionary<ulong, int> sizeHints, KnownClrMemoryPointer k)
        {
            if (sizeHints.TryGetValue(k.Pointer, out int hint))
                if ((ulong)hint > k.Size)
                    return (ulong)hint;

            return k.Size;
        }

        private class GCObjectToRange
        {
            public ulong SegmentPointer { get; }
            public ClrObject Object { get; set; }
            public AddressMemoryRange NativeMemoryRange { get; }

            public GCObjectToRange(ulong gcaddr, AddressMemoryRange nativeMemory)
            {
                SegmentPointer = gcaddr;
                NativeMemoryRange = nativeMemory;
            }
        }

        private class KnownClrMemoryPointer
        {
            private const string NativeHeapMemoryBlock = "System.Reflection.Internal.NativeHeapMemoryBlock";
            private const string MetadataReader = "System.Reflection.Metadata.MetadataReader";
            private const string NativeHeapMemoryBlockDisposableData = "System.Reflection.Internal.NativeHeapMemoryBlock+DisposableData";
            private const string ExternalMemoryBlockProvider = "System.Reflection.Internal.ExternalMemoryBlockProvider";
            private const string ExternalMemoryBlock = "System.Reflection.Internal.ExternalMemoryBlock";
            private const string RuntimeParameterInfo = "System.Reflection.RuntimeParameterInfo";

            public string Name => Object.Type?.Name ?? "<unknown_type>";
            public ClrObject Object { get; }
            public ulong Pointer { get; }
            public ulong Size { get; }

            public KnownClrMemoryPointer(ClrObject obj, nint pointer, int size)
            {
                Object = obj;
                Pointer = (ulong)pointer;
                Size = (ulong)size;
            }

            public static bool ContainsKnownClrMemoryPointers(ClrObject obj)
            {
                string? typeName = obj.Type?.Name;
                return typeName == NativeHeapMemoryBlock
                    || typeName == MetadataReader
                    || typeName == NativeHeapMemoryBlockDisposableData
                    || typeName == ExternalMemoryBlockProvider
                    || typeName == ExternalMemoryBlock
                    || typeName == RuntimeParameterInfo
                    ;
            }

            public static IEnumerable<KnownClrMemoryPointer> EnumerateKnownClrMemoryPointers(ClrObject obj, Dictionary<ulong, int> sizeHints)
            {
                switch (obj.Type?.Name)
                {
                    case RuntimeParameterInfo:
                        {
                            const int MDInternalROSize = 0x5f8; // Doesn't have to be exact
                            nint pointer = obj.ReadValueTypeField("m_scope").ReadField<nint>("m_metadataImport2");
                            AddSizeHint(sizeHints, pointer, MDInternalROSize);

                            yield return new KnownClrMemoryPointer(obj, pointer, MDInternalROSize);
                        }
                        break;
                    case ExternalMemoryBlock:
                        {
                            nint pointer = obj.ReadField<nint>("_buffer");
                            int size = obj.ReadField<int>("_size");

                            if (pointer != 0 && size > 0)
                                AddSizeHint(sizeHints, pointer, size);

                            yield return new KnownClrMemoryPointer(obj, pointer, size);
                        }
                        break;

                    case ExternalMemoryBlockProvider:
                        {
                            nint pointer = obj.ReadField<nint>("_memory");
                            int size = obj.ReadField<int>("_size");

                            if (pointer != 0 && size > 0)
                                AddSizeHint(sizeHints, pointer, size);

                            yield return new KnownClrMemoryPointer(obj, pointer, size);
                        }
                        break;

                    case NativeHeapMemoryBlockDisposableData:
                        {
                            nint pointer = obj.ReadField<nint>("_pointer");
                            sizeHints.TryGetValue((ulong)pointer, out int size);
                            yield return new KnownClrMemoryPointer(obj, pointer, size);
                        }
                        break;

                    case NativeHeapMemoryBlock:
                        {
                            // Just here for size hints

                            ClrObject pointerObject = obj.ReadObjectField("_data");
                            nint pointer = pointerObject.ReadField<nint>("_pointer");
                            int size = obj.ReadField<int>("_size");

                            if (pointer != 0 && size > 0)
                                AddSizeHint(sizeHints, pointer, size);
                        }

                        break;

                    case MetadataReader:
                        {
                            MemoryBlockImpl block = obj.ReadField<MemoryBlockImpl>("Block");
                            if (block.Pointer != 0 && block.Size > 0)
                                yield return new KnownClrMemoryPointer(obj, block.Pointer, block.Size);
                        }
                        break;
                }
            }

            private static void AddSizeHint(Dictionary<ulong, int> sizeHints, nint pointer, int size)
            {
                if (pointer != 0 && size != 0)
                {
                    ulong ptr = (ulong)pointer;

                    if (sizeHints.TryGetValue(ptr, out int hint))
                    {
                        if (hint < size)
                            sizeHints[ptr] = size;
                    }
                    else
                    {
                        sizeHints[ptr] = size;
                    }
                }
            }

            private readonly struct MemoryBlockImpl
            {
                public readonly nint Pointer { get; }
                public readonly int Size { get; }
            }
        }
    }
}
