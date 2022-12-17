using Microsoft.Diagnostics.Runtime.DacInterface;
using System.Diagnostics;
using System.Text;

namespace DbgEngExtension
{
    public class MAddress : DbgEngCommand
    {
        internal const string Command = "maddress";
        private const string IncludeReserveOption = "-includeReserve";
        private const string NoHeuristicOption = "-noReserveHeuristic";
        private const string ShowImageTableOption = "-showImages";
        private const string SummaryOption = "-summary";
        private const string StatOption = "-stat";

        public MAddress(nint pUnknown, bool redirectConsoleOutput = true)
            : base(pUnknown, redirectConsoleOutput)
        {
        }

        public MAddress(IDisposable dbgeng, bool redirectConsoleOutput = false)
            : base(dbgeng, redirectConsoleOutput)
        {
        }

        internal void Run(string args)
        {
            if (ParseArgs(args, out bool printAllRanges, out bool showImageTable, out bool includeReserveMemory, out bool tagReserveMemoryHeuristically))
                PrintMemorySummary(printAllRanges, showImageTable, includeReserveMemory, tagReserveMemoryHeuristically);
        }

        private static bool ParseArgs(string args, out bool printAllRanges, out bool showImageTable, out bool includeReserveMemory, out bool tagReserveMemoryHeuristically)
        {
            printAllRanges = true;
            showImageTable = false;
            includeReserveMemory = false;
            tagReserveMemoryHeuristically = true;

            if (string.IsNullOrWhiteSpace(args))
                return true;

            string[] parameters = args.Replace('/', '-').Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string? badEntry = parameters.Where(f => !f.StartsWith('-')).FirstOrDefault();
            if (badEntry is not null)
            {
                PrintUsage(badEntry);
                return false;
            }

            foreach (string word in parameters)
            {
                if (word.Equals(IncludeReserveOption, StringComparison.OrdinalIgnoreCase))
                    includeReserveMemory = true;

                else if (word.Equals(NoHeuristicOption, StringComparison.OrdinalIgnoreCase))
                    tagReserveMemoryHeuristically = false;

                else if (word.Equals(ShowImageTableOption, StringComparison.OrdinalIgnoreCase))
                    showImageTable = true;

                else if (word.Equals(SummaryOption, StringComparison.OrdinalIgnoreCase) || word.Equals(StatOption, StringComparison.OrdinalIgnoreCase))
                    printAllRanges = false;

                else
                {
                    PrintUsage(word);
                    return false;
                }
            }

            return true;
        }

        private static void PrintUsage(string? badEntry)
        {
            if (badEntry is not null)
                Console.WriteLine($"Unknown parameter: {badEntry}");

            Console.WriteLine($"usage: !{Command} [{SummaryOption}] [{ShowImageTableOption}] [{IncludeReserveOption}] [{NoHeuristicOption}]");
        }

        public void PrintMemorySummary(bool printAllMemory, bool showImageTable, bool includeReserveMemory, bool tagReserveMemoryHeuristically)
        {
            IEnumerable<AddressMemoryRange> memoryRanges = EnumerateAddressSpace(tagClrMemoryRanges: true, includeReserveMemory, tagReserveMemoryHeuristically);
            if (!includeReserveMemory)
                memoryRanges = memoryRanges.Where(m => m.State != MemState.MEM_RESERVE);

            AddressMemoryRange[] ranges = memoryRanges.ToArray();

            // Tag reserved memory based on what's adjacent.
            if (tagReserveMemoryHeuristically)
                CollapseReserveRegions(ranges);

            if (printAllMemory)
            {
                WriteHeader(5, new int[] { 12, 12, 12, 12, 12, 32, -1 }, "StartAddr", "EndAddr-1", "Length", "Type", "State", "Protect", "Usage");
                
                foreach (var mem in ranges)
                {
                    string description = mem.Description;
                    if (mem.ClrMemoryKind != ClrMemoryKind.None)
                        description = mem.ClrMemoryKind.ToString();

                    Console.WriteLine($"{mem.Start,12:x}     {mem.End,12:x}     {mem.Length.ConvertToHumanReadable(),12:x}     {mem.Kind,12}     {mem.State,12}     {mem.Protect,32}     {description}");
                }

                WriteBreak();
            }

            if (showImageTable)
            {
                int maxModuleLen = ranges.Where(r => !string.IsNullOrWhiteSpace(r.Image)).Max(r => r.Image.Length);

                var imageGroups = from mem in ranges.Where(r => r.State != MemState.MEM_RESERVE && r.Image != null)
                                  group mem by mem.Image into g
                                  let Size = g.Sum(k => (long)(k.End - k.Start))
                                  orderby Size descending
                                  select new
                                  {
                                      Image = g.Key,
                                      Count = g.Count(),
                                      Size
                                  };

                WriteHeader(1, new int[] { maxModuleLen, 12, 12, 24 }, "Image", "Regions", "Size", "Size (bytes)");

                int count = 0;
                long size = 0;
                foreach (var item in imageGroups)
                {
                    string image = item.Image + new string(' ', maxModuleLen - item.Image.Length);
                    Console.WriteLine($"{image} {item.Count,12:n0} {item.Size.ConvertToHumanReadable(),12} {item.Size,24:n0}");

                    count += item.Count;
                    size += item.Size;
                }

                WriteHeader(1, new int[] { maxModuleLen, 12, 12, 24 }, "TOTAL", count.ToString("n0"), size.ConvertToHumanReadable(), size.ToString("n0"));
                WriteBreak();
            }


            // Print summary table unconditionally
            {
                var grouped = from mem in ranges
                              let type = GetMemoryName(mem)
                              group mem by type into g
                              let Count = g.Count()
                              let Size = g.Sum(f => (long)(f.End - f.Start))
                              orderby Size descending
                              select new
                              {
                                  Type = g.Key,
                                  Count,
                                  Size
                              };

                WriteHeader(5, new int[] { 24, 12, 12, 24 }, "TypeSummary", "RngCount", "Size", "Size (bytes)");

                int count = 0;
                long size = 0;
                foreach (var item in grouped)
                {
                    Console.WriteLine($"{item.Type,24}     {item.Count,12:n0}     {item.Size.ConvertToHumanReadable(),12}     {item.Size,24:n0}");
                    count += item.Count;
                    size += item.Size;
                }

                WriteHeader(5, new int[] { 24, 12, 12, 24 }, "TOTAL", count.ToString("n0"), size.ConvertToHumanReadable(), size.ToString("n0"));
            }
        }

        private static void WriteHeader(int sepLength, int[] widths, params string[] columnNames)
        {
            if (widths.Length != columnNames.Length)
                throw new ArgumentException($"{nameof(widths)}.Length != {nameof(columnNames)}.Length");

            string separator = new('-', sepLength);
            StringBuilder sb = new();
            bool first = true;

            for (int i = 0; i < widths.Length; i++)
            {
                if (!first)
                    sb.Append(separator);

                string column = columnNames[i];
                int width = widths[i];

                if (width > 0)
                {
                    if (column.Length > width)
                        column = column[0..width];

                    int padding = width - column.Length;
                    if (padding > 0)
                        sb.Append('-', padding);
                    else if (padding < 0)
                        column = column[0..width];
                }

                sb.Append(column);
                first = false;
            }

            if (sb.Length < 79)
                sb.Append('-', 79 - sb.Length);

            Console.WriteLine(sb.ToString());
        }

        private static void WriteBreak()
        {
            Console.WriteLine(" ");
        }

        /// <summary>
        /// Enumerates the entire address space, optionally tagging special CLR heaps, and optionally "collapsing"
        /// MEM_RESERVE regions with a heuristic to blame them on the MEM_COMMIT region that came before it.
        /// See <see cref="CollapseReserveRegions"/> for more info.
        /// </summary>
        /// <param name="tagClrMemoryRanges">Whether to "tag" regions with CLR memory for more details.</param>
        /// <param name="includeReserveMemory">Whether to include MEM_RESERVE memory or not in the enumeration.</param>
        /// <param name="tagReserveMemoryHeuristically">Whether to heuristically "blame" MEM_RESERVE regions on what
        /// lives before it in the address space. For example, if there is a MEM_COMMIT region followed by a MEM_RESERVE
        /// region in the address space, this function will "blame" the MEM_RESERVE region on whatever type of memory
        /// the MEM_COMMIT region happens to be.  Usually this will be correct (e.g. the native heap will reserve a
        /// large chunk of memory and commit the beginning of it as it allocates more and more memory...the RESERVE
        /// region was actually "caused" by the Heap space before it).  Sometimes this will simply be wrong when
        /// a MEM_COMMIT region is next to an unrelated MEM_RESERVE region.
        /// 
        /// This is a heuristic, so use it accordingly.</param>
        /// <exception cref="InvalidOperationException">If !analyze fails we will throw InvalidOperationException.  This is usually
        /// because symbols for ntdll couldn't be found.</exception>
        /// <returns>An enumerable of memory ranges.</returns>
        public IEnumerable<AddressMemoryRange> EnumerateAddressSpace(bool tagClrMemoryRanges, bool includeReserveMemory, bool tagReserveMemoryHeuristically)
        {
            IEnumerable<AddressMemoryRange> addressResult = EnumerateBangAddress().Where(m => m.State != MemState.MEM_FREE);

            addressResult = addressResult.Where(m => (m.Start & 0xffffffff00000000) != 0xffffffff00000000 && (m.End & 0xffffffff00000000) != 0xffffffff00000000);

            if (!includeReserveMemory)
                addressResult = addressResult.Where(m => m.State != MemState.MEM_RESERVE);

            AddressMemoryRange[] ranges = addressResult.OrderBy(r => r.Start).ToArray();
            if (tagClrMemoryRanges)
            {
                foreach (ClrMemoryPointer mem in EnumerateClrMemoryAddresses())
                {
                    var found = ranges.Where(m => m.Start <= mem.Address && mem.Address < m.End).ToArray();

                    if (found.Length == 0)
                        Trace.WriteLine($"Warning:  Could not find a memory range for {mem.Address:x} - {mem.Kind}.");
                    else if (found.Length > 1)
                        Trace.WriteLine($"Warning:  Found multiple memory ranges for entry {mem.Address:x} - {mem.Kind}.");

                    foreach (var entry in found)
                    {
                        if (entry.ClrMemoryKind != ClrMemoryKind.None && entry.ClrMemoryKind != mem.Kind)
                            Trace.WriteLine($"Warning:  Overwriting range {entry.Start:x} {entry.ClrMemoryKind} -> {mem.Kind}.");

                        entry.ClrMemoryKind = mem.Kind;
                    }
                }
            }

            if (tagReserveMemoryHeuristically)
            {
                foreach (AddressMemoryRange mem in ranges)
                {
                    string memName = GetMemoryName(mem);
                    if (memName == "RESERVED")
                        TagMemoryRecursive(mem, ranges);
                }
            }

            return ranges;
        }

        /// <summary>
        /// This method heuristically tries to "blame" MEM_RESERVE regions on what lives before it on the heap.
        /// For example, if there is a MEM_COMMIT region followed by a MEM_RESERVE region in the address space,
        /// this function will "blame" the MEM_RESERVE region on whatever type of memory the MEM_COMMIT region
        /// happens to be.  Usually this will be correct (e.g. the native heap will reserve a large chunk of
        /// memory and commit the beginning of it as it allocates more and more memory...the RESERVE region
        /// was actually "caused" by the Heap space before it).  Sometimes this will simply be wrong when
        /// a MEM_COMMIT region is next to an unrelated MEM_RESERVE region.
        /// 
        /// This is a heuristic, so use it accordingly.
        /// </summary>
        public void CollapseReserveRegions(AddressMemoryRange[] ranges)
        {
            foreach (AddressMemoryRange mem in ranges)
            {
                string memName = GetMemoryName(mem);
                if (memName == "RESERVED")
                    TagMemoryRecursive(mem, ranges);
            }
        }

        /// <summary>
        /// Enumerates pointers to various CLR heaps in memory.
        /// </summary>
        public IEnumerable<ClrMemoryPointer> EnumerateClrMemoryAddresses()
        {
            Console.WriteLine("here");
            foreach (var runtime in Runtimes)
            {
                SOSDac sos = runtime.DacLibrary.SOSDacInterface;
                foreach (JitManagerInfo jitMgr in sos.GetJitManagers())
                {
                    foreach (var mem in sos.GetCodeHeapList(jitMgr.Address))
                        yield return new ClrMemoryPointer()
                        {
                            Address = mem.Address,
                            Kind = mem.Type switch
                            {
                                CodeHeapType.Loader => ClrMemoryKind.LoaderHeap,
                                CodeHeapType.Host => ClrMemoryKind.Host,
                                _ => ClrMemoryKind.UnknownCodeHeap
                            }
                        };

                    foreach (var seg in runtime.Heap.Segments)
                    {
                        if (seg.CommittedMemory.Length > 0)
                            yield return new ClrMemoryPointer() { Address = seg.CommittedMemory.Start, Kind = ClrMemoryKind.GCHeapSegment };

                        if (seg.ReservedMemory.Length > 0)
                            yield return new ClrMemoryPointer() { Address = seg.ReservedMemory.Start, Kind = ClrMemoryKind.GCHeapReserve };
                    }

                    foreach (ClrDataAddress address in sos.GetAppDomainList())
                    {
                        List<ClrMemoryPointer> heaps = new List<ClrMemoryPointer>();
                        if (sos.GetAppDomainData(address, out AppDomainData domain))
                        {
                            sos.TraverseLoaderHeap(domain.StubHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                            {
                                Address = address,
                                Kind = ClrMemoryKind.StubHeap
                            }));

                            sos.TraverseLoaderHeap(domain.HighFrequencyHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                            {
                                Address = address,
                                Kind = ClrMemoryKind.HighFrequencyHeap
                            }));

                            sos.TraverseLoaderHeap(domain.LowFrequencyHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                            {
                                Address = address,
                                Kind = ClrMemoryKind.LowFrequencyHeap
                            }));

                            sos.TraverseStubHeap(address, (int)VCSHeapType.IndcellHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                            {
                                Address = address,
                                Kind = ClrMemoryKind.IndcellHeap
                            }));


                            sos.TraverseStubHeap(address, (int)VCSHeapType.LookupHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                            {
                                Address = address,
                                Kind = ClrMemoryKind.LookupHeap
                            }));


                            sos.TraverseStubHeap(address, (int)VCSHeapType.ResolveHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                            {
                                Address = address,
                                Kind = ClrMemoryKind.ResolveHeap
                            }));


                            sos.TraverseStubHeap(address, (int)VCSHeapType.DispatchHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                            {
                                Address = address,
                                Kind = ClrMemoryKind.DispatchHeap
                            }));

                            sos.TraverseStubHeap(address, (int)VCSHeapType.CacheEntryHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                            {
                                Address = address,
                                Kind = ClrMemoryKind.CacheEntryHeap
                            }));
                        }

                        foreach (var heap in heaps)
                            yield return heap;
                    }
                }

                Console.WriteLine("done");
            }
        }

        /// <summary>
        /// Enumerates the output of !analyze in a usable format.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">If !analyze fails we will throw InvalidOperationException.  This is usually
        /// because symbols for ntdll couldn't be found.</exception>
        public IEnumerable<AddressMemoryRange> EnumerateBangAddress()
        {
            bool foundHeader = false;
            bool skipped = false;

            (int hr, string text) = RunCommandWithOutput("!address");
            if (hr < 0)
                throw new InvalidOperationException($"!address failed with hresult={hr:x}");

            foreach (string line in text.Split('\n'))
            {
                if (line.Length == 0)
                    continue;

                if (!foundHeader)
                {
                    // find the !address header
                    string[] split = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length > 0)
                        foundHeader = split[0] == "BaseAddress" && split.Last() == "Usage";
                }
                else if (!skipped)
                {
                    // skip the ---------- line
                    skipped = true;
                }
                else
                {
                    string[] parts = ((line[0] == '+') ? line[1..] : line).Split(new char[] { ' ' }, 6, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    ulong start = ulong.Parse(parts[0].Replace("`", ""), System.Globalization.NumberStyles.HexNumber);
                    ulong end = ulong.Parse(parts[1].Replace("`", ""), System.Globalization.NumberStyles.HexNumber);

                    int index = 3;
                    if (Enum.TryParse(parts[index], ignoreCase: true, out MemKind kind))
                        index++;

                    if (Enum.TryParse(parts[index], ignoreCase: true, out MemState state))
                        index++;

                    StringBuilder sbRemainder = new();
                    for (int i = index; i < parts.Length; i++)
                    {
                        if (i != index)
                            sbRemainder.Append(' ');

                        sbRemainder.Append(parts[i]);
                    }

                    string remainder = sbRemainder.ToString();
                    parts = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    MemProtect protect = default;
                    index = 0;
                    while (index < parts.Length - 1)
                    {
                        if (Enum.TryParse(parts[index], ignoreCase: true, out MemProtect result))
                        {
                            protect |= result;
                            if (parts[index + 1] == "|")
                                index++;
                        }
                        else
                        {
                            break;
                        }

                        index++;
                    }

                    string description = parts[index++];

                    // On Linux, !analyze is reporting this as MEM_PRIVATE or MEM_UNKNOWN
                    if (description == "Image")
                        kind = MemKind.MEM_IMAGE;

                    // On Linux, !analyze is reporting this as nothing
                    if (kind == MemKind.MEM_UNKNOWN && state == MemState.MEM_UNKNOWN && protect == MemProtect.PAGE_UNKNOWN)
                    {
                        state = MemState.MEM_FREE;
                        protect = MemProtect.PAGE_NOACCESS;
                    }

                    string? image = null;
                    if (kind == MemKind.MEM_IMAGE)
                        image = parts[index++][1..^1];

                    if (description.Equals("<unknown>", StringComparison.OrdinalIgnoreCase))
                        description = "";

                    yield return new AddressMemoryRange()
                    {
                        Start = start,
                        End = end,
                        Kind = kind,
                        State = state,
                        Protect = protect,
                        Description = description,
                        Image = image
                    };
                }
            }

            if (!foundHeader)
                throw new InvalidOperationException($"!address did not produce a standard header.\nThis may mean symbols could not be resolved for ntdll.\nPlease run !address and make sure the output looks correct.");
        }

        private static AddressMemoryRange? TagMemoryRecursive(AddressMemoryRange mem, AddressMemoryRange[] ranges)
        {
            string type = GetMemoryName(mem);
            if (type != "RESERVED")
                return mem;

            AddressMemoryRange? found = ranges.SingleOrDefault(r => r.End == mem.Start);
            if (found is null)
                return null;

            AddressMemoryRange? nonReserved = TagMemoryRecursive(found, ranges);
            if (nonReserved is null)
                return null;

            mem.Description = GetMemoryName(nonReserved);
            return nonReserved;
        }

        private static string GetMemoryName(AddressMemoryRange mem)
        {
            if (mem.ClrMemoryKind != ClrMemoryKind.None)
                return mem.ClrMemoryKind.ToString();

            if (!string.IsNullOrWhiteSpace(mem.Description))
                return mem.Description;

            if (mem.State == MemState.MEM_RESERVE)
                return "RESERVED";
            else if (mem.State == MemState.MEM_FREE)
                return "FREE";

            string result = mem.Protect.ToString();
            if (mem.Kind == MemKind.MEM_MAPPED)
            {
                if (string.IsNullOrWhiteSpace(result))
                    result = mem.Kind.ToString();
                else
                    result = result.Replace("PAGE", "MAPPED");
            }

            return result;
        }

        public enum ClrMemoryKind
        {
            None,
            LoaderHeap,
            Host,
            UnknownCodeHeap,
            GCHeapSegment,
            GCHeapReserve,
            StubHeap,
            HighFrequencyHeap,
            LowFrequencyHeap,
            IndcellHeap,
            LookupHeap,
            ResolveHeap,
            DispatchHeap,
            CacheEntryHeap,
        }

        enum VCSHeapType
        {
            IndcellHeap,
            LookupHeap,
            ResolveHeap,
            DispatchHeap,
            CacheEntryHeap
        }

        [Flags]
        public enum MemProtect
        {
            PAGE_UNKNOWN = 0,
            PAGE_EXECUTE = 0x00000010,
            PAGE_EXECUTE_READ = 0x00000020,
            PAGE_EXECUTE_READWRITE = 0x00000040,
            PAGE_EXECUTE_WRITECOPY = 0x00000080,
            PAGE_NOACCESS = 0x00000001,
            PAGE_READONLY = 0x00000002,
            PAGE_READWRITE = 0x00000004,
            PAGE_WRITECOPY = 0x00000008,
            PAGE_GUARD = 0x00000100,
            PAGE_NOCACHE = 0x00000200,
            PAGE_WRITECOMBINE = 0x00000400
        }

        public enum MemState
        {
            MEM_UNKNOWN = 0,
            MEM_COMMIT = 0x1000,
            MEM_FREE = 0x10000,
            MEM_RESERVE = 0x2000
        }

        public enum MemKind
        {
            MEM_UNKNOWN = 0,
            MEM_IMAGE = 0x1000000,
            MEM_MAPPED = 0x40000,
            MEM_PRIVATE = 0x20000
        }

        public class AddressMemoryRange
        {
            public ulong Start { get; internal set; }
            public ulong End { get; internal set; }

            public ulong Length => End <= Start ? 0 : End - Start;

            public MemKind Kind { get; internal set; }
            public MemState State { get; internal set; }
            public MemProtect Protect { get; internal set; }
            public string Description { get; internal set; } = "";
            public ClrMemoryKind ClrMemoryKind { get; internal set; }
            public string? Image { get; internal set; }
        }

        public class ClrMemoryPointer
        {
            public ulong Address { get; internal set; }
            public ClrMemoryKind Kind { get; internal set; }
        }
    }
}
