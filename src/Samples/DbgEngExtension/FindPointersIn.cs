// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.DbgEng;
using static DbgEngExtension.MAddress;

namespace DbgEngExtension
{
    public class FindPointersIn : DbgEngCommand
    {
        private const int Width = 120;

        internal const string Command = "findpointersin";
        internal const string ExpandNonPinnedObjectsFlag = "expandNonPinnedObjects";
        private const string VtableConst = "vtable for ";

        public FindPointersIn(nint pUnknown, bool redirectConsoleOutput = true)
            : base(pUnknown, redirectConsoleOutput)
        {
        }

        public FindPointersIn(IDisposable dbgeng, bool redirectConsoleOutput = false)
            : base(dbgeng, redirectConsoleOutput)
        {
        }

        internal void Run(string strArgs)
        {
            string[] types = strArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                .GetOptionalFlag(ExpandNonPinnedObjectsFlag, out bool showAll);

            PrintPointers(pinnedOnly: !showAll, types);
        }

        public void PrintPointers(bool pinnedOnly, params string[] memTypes)
        {
            MAddress address = new(this);
            AddressMemoryRange[] allRegions = address.EnumerateAddressSpace(tagClrMemoryRanges: true, includeReserveMemory: false, tagReserveMemoryHeuristically: false).ToArray();

            Console.WriteLine("Scanning for pinned objects...");
            MemoryWalkContext ctx = CreateMemoryWalkContext();

            foreach (string type in memTypes)
            {
                AddressMemoryRange[] matchingRanges = allRegions.Where(r => r.Name == type).ToArray();
                if (matchingRanges.Length == 0)
                {
                    Console.WriteLine($"Found not memory regions matching '{type}'.");
                    continue;
                }

                RegionPointers totals = new();

                foreach (AddressMemoryRange mem in matchingRanges.OrderBy(r => r.Start))
                {
                    IEnumerable<(ulong Pointer, AddressMemoryRange MemoryRange)> pointersFound = address.EnumerateRegionPointers(mem.Start, mem.End, allRegions).Select(r => (r.Pointer, r.MemoryRange));
                    RegionPointers result = ProcessOneRegion(pinnedOnly, pointersFound, ctx);

                    WriteMemoryHeaderLine(mem);

                    Console.WriteLine($"Type:  {mem.Name}");
                    if (mem.Image is not null)
                        Console.WriteLine($"Image: {mem.Image}");

                    WriteTables(ctx, result, false);

                    Console.WriteLine();
                    Console.WriteLine("END REGION".PadLeft((Width - 10) / 2, '=').PadRight(Width, '='));
                    Console.WriteLine();

                    result.AddTo(totals);
                }

                if (matchingRanges.Length > 1)
                {
                    Console.WriteLine(" TOTALS ".PadLeft(Width / 2).PadRight(Width));

                    WriteTables(ctx, totals, truncate: true);

                    Console.WriteLine(new string('=', Width));
                }
            }
        }

        private void WriteTables(MemoryWalkContext ctx, RegionPointers result, bool truncate)
        {
            if (result.PointersToGC > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Pointers to GC heap:");

                PrintGCPointerTable(result);
            }

            if (result.ResolvablePointers.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Pointers to images with symbols:");

                WriteResolvablePointerTable(ctx, result, truncate);
            }

            if (result.UnresolvablePointers.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Other pointers:");

                WriteUnresolvablePointerTable(result, truncate);
            }
        }

        private static void WriteMemoryHeaderLine(AddressMemoryRange mem)
        {
            string header = $"REGION [{mem.Start:x}-{mem.End:x}] {mem.Kind} {mem.State} {mem.Protect}";
            int lpad = (Width - header.Length) / 2;
            if (lpad > 0)
                header = header.PadLeft(Width - lpad, '=');
            Console.WriteLine(header.PadRight(Width, '='));
        }

        private static void PrintGCPointerTable(RegionPointers result)
        {
            if (result.PinnedPointers.Count == 0)
            {
                Console.WriteLine($"{result.PointersToGC:n0} pointers to the GC heap, but none pointed to a pinned object.");
            }
            else
            {
                IEnumerable<(string Key, int, int, IEnumerable<ulong>)> gcResult = from obj in result.PinnedPointers
                                                                                   let name = obj.Type?.Name ?? "<unknown_object_types>"
                                                                                   group obj.Address by name into g
                                                                                   let Count = g.Count()
                                                                                   orderby Count descending
                                                                                   select
                                                                                   (
                                                                                       g.Key,
                                                                                       Count,
                                                                                       new HashSet<ulong>(g).Count,
                                                                                       g.AsEnumerable()
                                                                                   );

                if (result.NonPinnedGCPointers.Count > 0)
                {
                    (string, int, int, IEnumerable<ulong>)[] v = new (string, int, int, IEnumerable<ulong>)[] { ("[Pointers to non-pinned objects]", result.NonPinnedGCPointers.Count, new HashSet<ulong>(result.NonPinnedGCPointers).Count, result.NonPinnedGCPointers) };
                    gcResult = v.Concat(gcResult);
                }

                PrintPointerTable("Type", "[Other Pinned Object Pointers]", forceTruncate: false, gcResult);
            }
        }

        private static void WriteUnresolvablePointerTable(RegionPointers result, bool forceTruncate)
        {
            IEnumerable<(string Key, int, int, IEnumerable<ulong>)> unresolvedQuery = from item in result.UnresolvablePointers
                                                                                      let Name = item.Key.Image ?? item.Key.Name
                                                                                      group item.Value by Name into g
                                                                                      let All = g.SelectMany(r => r).ToArray()
                                                                                      let Count = All.Length
                                                                                      orderby Count descending
                                                                                      select
                                                                                      (
                                                                                          g.Key,
                                                                                          Count,
                                                                                          new HashSet<ulong>(All).Count,
                                                                                          All.AsEnumerable()
                                                                                      );


            PrintPointerTable("Region", "[Unique Pointers to Unique Regions]", forceTruncate, unresolvedQuery);
        }

        private void WriteResolvablePointerTable(MemoryWalkContext ctx, RegionPointers result, bool forceTruncate)
        {
            IEnumerable<(string, int, int, IEnumerable<ulong>)> resolvedQuery = from ptr in result.ResolvablePointers.SelectMany(r => r.Value)
                                                                                let r = ctx.ResolveSymbol(DebugSymbols, ptr)
                                                                                let name = r.Symbol ?? "<unknown_function>"
                                                                                group (ptr, r.Offset) by name into g
                                                                                let Count = g.Count()
                                                                                let UniqueOffsets = new HashSet<int>(g.Select(g => g.Offset))
                                                                                orderby Count descending
                                                                                select
                                                                                (
                                                                                    FixTypeName(g.Key, UniqueOffsets),
                                                                                    Count,
                                                                                    UniqueOffsets.Count,
                                                                                    g.Select(r => r.ptr)
                                                                                );

            PrintPointerTable("Symbol", "[Unique Pointers]", forceTruncate, resolvedQuery);
        }

        private static void PrintPointerTable(string nameColumn, string truncatedName, bool forceTruncate, IEnumerable<(string Name, int Count, int Unique, IEnumerable<ulong> Pointers)> query)
        {
            (string Name, int Count, int Unique, IEnumerable<ulong> Pointers)[] resolved = query.ToArray();
            if (resolved.Length == 0)
                return;

            int single = resolved.Count(r => r.Count == 1);
            int multi = resolved.Length - single;
            bool truncate = forceTruncate || (single + multi > 75 && single > multi);
            truncate = false;

            int maxNameLen = multi > 0 ? resolved.Where(r => !truncate || r.Count > 1).Max(r => r.Name.Length) : resolved.Max(r => r.Name.Length);
            int nameLen = Math.Min(80, maxNameLen);
            nameLen = Math.Max(nameLen, truncatedName.Length);

            TableOutput table = new((nameLen, ""), (12, "n0"), (12, "n0"), (12, "x"))
            {
                Divider = "   "
            };
            table.WriteRowWithSpacing('-', nameColumn, "Unique", "Count", "RndPtr");

            IEnumerable<(string Name, int Count, int Unique, IEnumerable<ulong> Pointers)> items = truncate ? resolved.Take(multi) : resolved;
            foreach ((string Name, int Count, int Unique, IEnumerable<ulong> Pointers) in items)
                table.WriteRow(Name, Unique, Count, FindMostCommonPointer(Pointers));

            if (truncate)
                table.WriteRow(truncatedName, single, single);

            table.WriteRowWithSpacing('-', " [ TOTALS ] ", resolved.Sum(r => r.Unique), resolved.Sum(r => r.Count), "");
        }

        private static string FixTypeName(string typeName, HashSet<int> offsets)
        {
            if (typeName.EndsWith('!') && typeName.Count(r => r == '!') == 1)
                typeName = typeName[..^1];

            int vtableIdx = typeName.IndexOf(VtableConst);
            if (vtableIdx > 0)
                typeName = typeName.Replace(VtableConst, "") + "::vtable";

            if (offsets.Count == 1 && offsets.Single() > 0)
                typeName = $"{typeName}+{offsets.Single():x}";
            else if (offsets.Count > 1)
                typeName = $"{typeName}+...";

            return typeName;
        }

        private RegionPointers ProcessOneRegion(bool pinnedOnly, IEnumerable<(ulong Pointer, AddressMemoryRange Range)> pointersFound, MemoryWalkContext ctx)
        {
            RegionPointers result = new();

            foreach ((ulong Pointer, AddressMemoryRange Range) found in pointersFound)
            {
                if (found.Range.ClrMemoryKind == ClrMemoryKind.GCHeapSegment)
                {
                    if (pinnedOnly)
                    {
                        if (ctx.IsPinnedObject(found.Pointer, out ClrObject obj))

                            result.AddGCPointer(obj);
                        else
                            result.AddGCPointer(found.Pointer);
                    }
                    else
                    {
                        ClrObject obj = Runtimes.Single().Heap.GetObject(found.Pointer);
                        if (obj.IsValid)
                            result.AddGCPointer(obj);
                    }
                }
                else if (found.Range.Kind == MemKind.MEM_IMAGE)
                {
                    result.AddRegionPointer(found.Range, found.Pointer, ctx.HasSymbols(DebugSymbols, found.Range));
                }
                else
                {
                    result.AddRegionPointer(found.Range, found.Pointer, hasSymbols: false);
                }
            }

            return result;
        }

        public MemoryWalkContext CreateMemoryWalkContext()
        {
            HashSet<ulong> seen = new();
            List<ClrObject> pinned = new();

            foreach (ClrRuntime runtime in Runtimes)
            {
                foreach (ClrRoot? root in runtime.Heap.EnumerateRoots().Where(r => r.IsPinned))
                {
                    if (root.Object.IsValid && !root.Object.IsFree)
                        if (seen.Add(root.Object))
                            pinned.Add(root.Object);
                }

                foreach (ClrSegment seg in runtime.Heap.Segments.Where(s => s.IsPinned))
                {
                    foreach (ClrObject obj in seg.EnumerateObjects().Where(o => seen.Add(o)))
                    {
                        if (!obj.IsFree && obj.IsValid)
                            pinned.Add(obj);
                    }
                }
            }

            return new MemoryWalkContext(pinned);
        }

        public class RegionPointers
        {
            public Dictionary<AddressMemoryRange, List<ulong>> ResolvablePointers { get; } = new();
            public Dictionary<AddressMemoryRange, List<ulong>> UnresolvablePointers { get; } = new();
            public List<ClrObject> PinnedPointers { get; } = new();
            public List<ulong> NonPinnedGCPointers { get; } = new();
            public long PointersToGC => PinnedPointers.Count + NonPinnedGCPointers.Count;

            public RegionPointers()
            {
            }

            public void AddGCPointer(ulong address)
            {
                NonPinnedGCPointers.Add(address);
            }

            public void AddGCPointer(ClrObject obj)
            {
                PinnedPointers.Add(obj);
            }

            internal void AddRegionPointer(AddressMemoryRange range, ulong pointer, bool hasSymbols)
            {
                Dictionary<AddressMemoryRange, List<ulong>> pointerMap = hasSymbols ? ResolvablePointers : UnresolvablePointers;

                if (!pointerMap.TryGetValue(range, out List<ulong>? pointers))
                    pointers = pointerMap[range] = new();

                pointers.Add(pointer);
            }

            public void AddTo(RegionPointers destination)
            {
                AddTo(ResolvablePointers, destination.ResolvablePointers);
                AddTo(UnresolvablePointers, destination.UnresolvablePointers);
                destination.PinnedPointers.AddRange(PinnedPointers);
                destination.NonPinnedGCPointers.AddRange(NonPinnedGCPointers);
            }

            private static void AddTo(Dictionary<AddressMemoryRange, List<ulong>> sourceDict, Dictionary<AddressMemoryRange, List<ulong>> destDict)
            {
                foreach (KeyValuePair<AddressMemoryRange, List<ulong>> item in sourceDict)
                {
                    if (destDict.TryGetValue(item.Key, out List<ulong>? values))
                        values.AddRange(item.Value);
                    else
                        destDict[item.Key] = new(item.Value);
                }
            }
        }

        public class MemoryWalkContext
        {
            private readonly Dictionary<string, bool> _imageByNameHasSymbols = new();
            private readonly Dictionary<AddressMemoryRange, bool> _imageHasSymbols = new();
            private readonly Dictionary<ulong, (string?, int)> _resolved = new();
            private readonly ClrObject[] _pinned;

            public MemoryWalkContext(IEnumerable<ClrObject> pinnedObjects)
            {
                _pinned = pinnedObjects.Where(o => o.IsValid && !o.IsFree).OrderBy(o => o.Address).ToArray();
            }

            public bool IsPinnedObject(ulong address, out ClrObject found)
            {
                ClrObject last = _pinned.LastOrDefault();
                if (_pinned.Length == 0 || address < _pinned[0].Address || address >= last.Address + last.Size)
                {
                    found = default;
                    return false;
                }

                int low = 0;
                int high = _pinned.Length - 1;
                while (low <= high)
                {
                    int mid = (low + high) >> 1;
                    if (_pinned[mid].Address + _pinned[mid].Size <= address)
                    {
                        low = mid + 1;
                    }
                    else if (address < _pinned[mid].Address)
                    {
                        high = mid - 1;
                    }
                    else
                    {
                        found = _pinned[mid];
                        return true;
                    }
                }

                found = default;
                return false;
            }

            public (string? Symbol, int Offset) ResolveSymbol(IDebugSymbols symbols, ulong ptr)
            {
                if (_resolved.TryGetValue(ptr, out (string?, int) result))
                    return result;

                // _resolved is just a cache.  Don't let it get so big we eat all of the memory.
                if (_resolved.Count > 16 * 1024)
                    _resolved.Clear();

                if (symbols.GetNameByOffset(ptr, out string? name, out ulong displacement))
                    return _resolved[ptr] = (name, displacement > int.MaxValue ? int.MaxValue : (int)displacement);

                return (null, -1);
            }


            public bool HasSymbols(IDebugSymbols symbols, AddressMemoryRange range)
            {
                if (_imageHasSymbols.TryGetValue(range, out bool hasSymbols))
                    return hasSymbols;

                ulong imageBase = FindBaseAddress(symbols, range.Start, out string? filename);

                if (imageBase == 0)
                    return _imageHasSymbols[range] = false;

                if (filename is not null && _imageByNameHasSymbols.TryGetValue(filename, out hasSymbols))
                    return hasSymbols;

                HResult hr = symbols.GetModuleParameters(imageBase, out DEBUG_MODULE_PARAMETERS param);
                if (hr && param.SymbolType != DEBUG_SYMTYPE.NONE && param.SymbolType != DEBUG_SYMTYPE.DEFERRED)
                    return SetValue(param.SymbolType, range, filename);

                if (filename is not null)
                {
                    string module = Path.GetFileName(filename);
                    hr = symbols.Reload(module);
                    if (!hr)
                    {
                        // Ugh, Reload might not like the module name that GetModuleName gives us.
                        symbols.GetNameByOffset(range.Start, out _, out _);
                    }
                }

                symbols.GetModuleParameters(imageBase, out param);
                return SetValue(param.SymbolType, range, filename);
            }

            private bool SetValue(DEBUG_SYMTYPE symbolType, AddressMemoryRange range, string? filename)
            {
                bool hasSymbols = symbolType is not DEBUG_SYMTYPE.DEFERRED and not DEBUG_SYMTYPE.NONE;
                _imageHasSymbols.Add(range, hasSymbols);

                if (filename is not null)
                    _imageByNameHasSymbols.Add(filename, hasSymbols);

                return hasSymbols;
            }

            private static ulong FindBaseAddress(IDebugSymbols symbols, ulong ptr, out string? filename)
            {
                int hr = symbols.GetModuleByOffset(ptr, 0, out _, out ulong baseAddr);
                if (hr == 0)
                {

                    if (symbols.GetModuleName(DEBUG_MODNAME.IMAGE, baseAddr, out filename) < 0)
                        filename = null;
                }
                else
                {
                    filename = "";
                }

                return baseAddr;
            }
        }
    }
}