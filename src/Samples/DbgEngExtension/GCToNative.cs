// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Microsoft.Diagnostics.Runtime;
using static DbgEngExtension.MAddress;

namespace DbgEngExtension
{
    // Commands for helping locate native memory held onto by the GC heap.
    public class GCToNative : DbgEngCommand
    {
        internal const string Command = "gctonative";
        internal const string All = "all";
        private const int Width = 120;

        public GCToNative(nint pUnknown, bool redirectConsoleOutput = true) : base(pUnknown, redirectConsoleOutput)
        {
        }

        public GCToNative(IDisposable dbgeng, bool redirectConsoleOutput = false) : base(dbgeng, redirectConsoleOutput)
        {
        }

        /// <summary>
        /// Find regions of memory that the GC heap points to.
        /// </summary>
        internal void Run(string args)
        {
            string[] types = args.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                 .GetOptionalFlag(All, out bool showAll);

            if (types.Length == 0)
                Console.WriteLine($"usage: !{Command} TYPES");
            else
                PrintGCPointersToMemory(showAll, types);
        }

        public void PrintGCPointersToMemory(bool showAll, params string[] memoryTypes)
        {
            // Strategy:
            //   1. Use ClrMD to get the bounds of the GC heap where objects are allocated.
            //   2. Manually read that memory and check every pointer-aligned address for pointers to the heap regions requested
            //      while recording pointers in a list as we go (along with which ClrSegment they came from).
            //   3. Walk each GC segment which has pointers to the target regions to find objects:
            //        a.  Annotate each target pointer so we know what object points to the region (and throw away any pointers
            //            that aren't in an object...those are pointers from dead or relocated objects).
            //        b.  We have some special knowledge of "well known types" here that contain pointers.  These types point to
            //            native memory and contain a size of the region they point to.  Record that information as we go.
            //   4. Use information from "well known types" about regions of memory to annotate other pointers that do not have
            //      size information.
            //   5. Display all of this to the user.

            if (memoryTypes.Length == 0)
                return;

            MAddress maddressHelper = new(this);
            IEnumerable<AddressMemoryRange> rangeEnum = maddressHelper.EnumerateAddressSpace(tagClrMemoryRanges: true, includeReserveMemory: false, tagReserveMemoryHeuristically: false);
            rangeEnum = rangeEnum.Where(r => memoryTypes.Any(memType => r.Name.Equals(memType, StringComparison.OrdinalIgnoreCase)));
            rangeEnum = rangeEnum.OrderBy(r => r.Start);

            AddressMemoryRange[] ranges = rangeEnum.ToArray();

            if (ranges.Length == 0)
            {
                Console.WriteLine($"No matching memory ranges.");
                Console.WriteLine();
                return;
            }

            Console.WriteLine("Walking GC heap to find pointers...");
            Dictionary<ClrSegment, List<GCObjectToRange>> segmentLists = new();

            IEnumerable<(ClrSegment Segment, ulong Address, ulong Pointer, AddressMemoryRange MemoryRange)> items = Runtimes
                            .SelectMany(r => r.Heap.Segments)
                            .SelectMany(Segment => maddressHelper
                                                    .EnumerateRegionPointers(Segment.ObjectRange.Start, Segment.ObjectRange.End, ranges)
                                                    .Select(regionPointer => (Segment, regionPointer.Address, regionPointer.Pointer, regionPointer.MemoryRange)));

            foreach ((ClrSegment Segment, ulong Address, ulong Pointer, AddressMemoryRange MemoryRange) item in items)
            {
                if (!segmentLists.TryGetValue(item.Segment, out List<GCObjectToRange>? list))
                    list = segmentLists[item.Segment] = new();

                list.Add(new GCObjectToRange(item.Address, item.Pointer, item.MemoryRange));
            }

            Console.WriteLine("Resolving object names...");
            foreach (string type in memoryTypes)
            {
                WriteHeader($" {type} Regions ");

                List<ulong> addressesNotInObjects = new();
                List<(ulong Pointer, ClrObject Object)> unknownObjPointers = new();
                Dictionary<ulong, KnownClrMemoryPointer> knownMemory = new();
                Dictionary<ulong, int> sizeHints = new();

                foreach (KeyValuePair<ClrSegment, List<GCObjectToRange>> segEntry in segmentLists)
                {
                    ClrSegment seg = segEntry.Key;
                    List<GCObjectToRange> pointers = segEntry.Value;
                    pointers.Sort((x, y) => x.GCPointer.CompareTo(y.GCPointer));

                    int index = 0;
                    foreach (ClrObject obj in seg.EnumerateObjects())
                    {
                        if (index >= pointers.Count)
                            break;

                        while (index < pointers.Count && pointers[index].GCPointer < obj.Address)
                        {
                            // If we "missed" the pointer then it's outside of an object range.
                            addressesNotInObjects.Add(pointers[index].GCPointer);

                            Trace.WriteLine($"Skipping {pointers[index].GCPointer:x} lastObj={obj.Address:x}-{obj.Address + obj.Size:x} {obj.Type?.Name}");

                            index++;
                        }

                        if (index == pointers.Count)
                            break;

                        while (index < pointers.Count && obj.Address <= pointers[index].GCPointer && pointers[index].GCPointer < obj.Address + obj.Size)
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

                                unknownObjPointers.Add((pointers[index].TargetSegmentPointer, obj));
                            }

                            index++;
                        }
                    }
                }

                Console.WriteLine();
                if (knownMemory.Count == 0 && unknownObjPointers.Count == 0)
                {
                    Console.WriteLine($"No GC heap pointers to '{type}' regions.");
                }
                else
                {
                    if (showAll)
                    {
                        Console.WriteLine($"All memory pointers:");

                        IEnumerable<(ulong Pointer, ulong Size, ulong Object, string Type)> allPointers = unknownObjPointers.Select(unknown => (unknown.Pointer, 0ul, unknown.Object.Address, unknown.Object.Type?.Name ?? "<unknown_type>"));
                        allPointers = allPointers.Concat(knownMemory.Values.Select(k => (k.Pointer, GetSize(sizeHints, k), k.Object.Address, k.Name)));

                        TableOutput allOut = new((16, "x"), (16, "x"), (16, "x"))
                        {
                            Divider = " | "
                        };

                        allOut.WriteRowWithSpacing('-', "Pointer", "Size", "Object", "Type");
                        foreach ((ulong Pointer, ulong Size, ulong Object, string Type) entry in allPointers)
                            if (entry.Size == 0)
                                allOut.WriteRow(entry.Pointer, "", entry.Object, entry.Type);
                            else
                                allOut.WriteRow(entry.Pointer, entry.Size, entry.Object, entry.Type);

                        Console.WriteLine();
                    }

                    if (knownMemory.Count > 0)
                    {
                        Console.WriteLine($"Well-known memory pointer summary:");

                        // totals
                        var knownMemorySummary = from known in knownMemory.Values
                                                 group known by known.Name into g
                                                 let Name = g.Key
                                                 let Count = g.Count()
                                                 let TotalSize = g.Sum(k => (long)GetSize(sizeHints, k))
                                                 orderby TotalSize descending, Name ascending
                                                 select new {
                                                     Name,
                                                     Count,
                                                     TotalSize,
                                                     Pointer = FindMostCommonPointer(g.Select(p => p.Pointer))
                                                 };

                        int maxNameLen = Math.Min(80, knownMemory.Values.Max(r => r.Name.Length));

                        TableOutput summary = new((-maxNameLen, ""), (8, "n0"), (12, "n0"), (12, "n0"), (12, "x"))
                        {
                            Divider = " | "
                        };

                        summary.WriteRowWithSpacing('-', "Type", "Count", "Size", "Size (bytes)", "RndPointer");

                        foreach (var item in knownMemorySummary)
                            summary.WriteRow(item.Name, item.Count, item.TotalSize.ConvertToHumanReadable(), item.TotalSize, item.Pointer);

                        (int totalRegions, ulong totalBytes) = GetSizes(knownMemory, sizeHints);

                        summary.WriteSpacer('-');
                        summary.WriteRow("[TOTAL]", totalRegions, totalBytes.ConvertToHumanReadable(), totalBytes);

                        Console.WriteLine();
                    }


                    if (unknownObjPointers.Count > 0)
                    {
                        Console.WriteLine($"Other memory pointer summary:");

                        var unknownMemQuery = from known in unknownObjPointers
                                              let name = CollapseGenerics(known.Object.Type?.Name ?? "<unknown_type>")
                                              group known by name into g
                                              let Name = g.Key
                                              let Count = g.Count()
                                              orderby Count descending
                                              select new {
                                                  Name,
                                                  Count,
                                                  Pointer = FindMostCommonPointer(g.Select(p => p.Pointer))
                                              };

                        var unknownMem = unknownMemQuery.ToArray();
                        int maxNameLen = Math.Min(80, unknownMem.Max(r => r.Name.Length));

                        TableOutput summary = new((-maxNameLen, ""), (8, "n0"), (12, "x"))
                        {
                            Divider = " | "
                        };

                        summary.WriteRowWithSpacing('-', "Type", "Count", "RndPointer");

                        foreach (var item in unknownMem)
                            summary.WriteRow(item.Name, item.Count, item.Pointer);
                    }
                }
            }
        }

        private static (int Regions, ulong Bytes) GetSizes(Dictionary<ulong, KnownClrMemoryPointer> knownMemory, Dictionary<ulong, int> sizeHints)
        {
            IOrderedEnumerable<KnownClrMemoryPointer> ordered = from item in knownMemory.Values
                                                                orderby item.Pointer ascending, item.Size descending
                                                                select item;

            int totalRegions = 0;
            ulong totalBytes = 0;
            ulong prevEnd = 0;

            foreach (KnownClrMemoryPointer? item in ordered)
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

            return (totalRegions, totalBytes);
        }

        private static void WriteHeader(string header)
        {
            int lpad = (Width - header.Length) / 2;
            if (lpad > 0)
                header = header.PadLeft(Width - lpad, '=');
            Console.WriteLine(header.PadRight(Width, '='));
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

        private sealed class GCObjectToRange
        {
            public ulong GCPointer { get; }
            public ulong TargetSegmentPointer { get; }
            public ClrObject Object { get; set; }
            public AddressMemoryRange NativeMemoryRange { get; }

            public GCObjectToRange(ulong gcaddr, ulong pointer, AddressMemoryRange nativeMemory)
            {
                GCPointer = gcaddr;
                TargetSegmentPointer = pointer;
                NativeMemoryRange = nativeMemory;
            }
        }

        private sealed class KnownClrMemoryPointer
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