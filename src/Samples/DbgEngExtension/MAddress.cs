// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using static Microsoft.Diagnostics.Runtime.DacInterface.SOSDac;

namespace DbgEngExtension
{
    /// <summary>
    /// Parses the output of !address and annotates it with information about the CLR Runtime's
    /// allocations and memory regions.
    /// </summary>
    public class MAddress : DbgEngCommand
    {
        internal const string Command = "maddress";

        private const string IncludeReserveOption = "-includeReserve";
        private const string UseHeuristicOption = "-useReserveHeuristic";
        private const string ShowImageTableOption = "-showImageTable";
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

        public MAddress(DbgEngCommand cmd)
            : base(cmd)
        {
        }
        internal void Run(string args)
        {
            if (ParseArgs(args, out bool printAllRanges, out bool showImageTable, out bool includeReserveMemory, out bool tagReserveMemoryHeuristically))
                PrintMemorySummary(printAllRanges, showImageTable, includeReserveMemory, tagReserveMemoryHeuristically);
        }

        public IEnumerable<(ulong Address, ulong Pointer, AddressMemoryRange MemoryRange)> EnumerateRegionPointers(ulong start, ulong end, AddressMemoryRange[] ranges)
        {
            ulong[] array = ArrayPool<ulong>.Shared.Rent(4096);
            int arrayBytes = array.Length * sizeof(ulong);
            try
            {
                ulong curr = start;
                ulong remaining = end - start;

                while (remaining > 0)
                {
                    int size = Math.Min(remaining > int.MaxValue ? int.MaxValue : (int)remaining, arrayBytes);
                    bool res = ReadMemory(curr, array, size, out int bytesRead);
                    if (!res || bytesRead <= 0)
                        break;

                    for (int i = 0; i < bytesRead / sizeof(ulong); i++)
                    {
                        ulong ptr = array[i];

                        AddressMemoryRange? found = FindMemory(ranges, ptr);
                        if (found is not null)
                            yield return (curr + (uint)i * sizeof(ulong), ptr, found);
                    }

                    curr += (uint)bytesRead;
                    remaining -= (uint)bytesRead; ;
                }

            }
            finally
            {
                ArrayPool<ulong>.Shared.Return(array);
            }
        }

        private static AddressMemoryRange? FindMemory(AddressMemoryRange[] ranges, ulong ptr)
        {
            if (ptr < ranges[0].Start || ptr >= ranges.Last().End)
                return null;

            int low = 0;
            int high = ranges.Length - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (ranges[mid].End <= ptr)
                {
                    low = mid + 1;
                }
                else if (ptr < ranges[mid].Start)
                {
                    high = mid - 1;
                }
                else
                {
                    return ranges[mid];
                }
            }

            return null;
        }

        private unsafe bool ReadMemory(ulong start, ulong[] array, int size, out int bytesRead)
        {
            bytesRead = 0;
            fixed (ulong* ptr = array)
            {
                Span<byte> buffer = new(ptr, size);
                return DebugDataSpaces.ReadVirtual(start, buffer, out bytesRead);
            }
        }


        private static bool ParseArgs(string args, out bool printAllRanges, out bool showImageTable, out bool includeReserveMemory, out bool tagReserveMemoryHeuristically)
        {
            printAllRanges = true;
            showImageTable = false;
            includeReserveMemory = false;
            tagReserveMemoryHeuristically = false;

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

                else if (word.Equals(UseHeuristicOption, StringComparison.OrdinalIgnoreCase))
                    tagReserveMemoryHeuristically = true;

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

            Console.WriteLine($"usage: !{Command} [{SummaryOption}] [{ShowImageTableOption}] [{IncludeReserveOption}] [{UseHeuristicOption}]");
        }

        public void PrintMemorySummary(bool printAllMemory, bool showImageTable, bool includeReserveMemory, bool tagReserveMemoryHeuristically)
        {
            IEnumerable<AddressMemoryRange> memoryRanges = EnumerateAddressSpace(tagClrMemoryRanges: true, includeReserveMemory, tagReserveMemoryHeuristically);
            if (!includeReserveMemory)
                memoryRanges = memoryRanges.Where(m => m.State != MemState.MEM_RESERVE);

            AddressMemoryRange[] ranges = memoryRanges.ToArray();

            int nameSizeMax = ranges.Max(r => r.Name.Length);

            // Tag reserved memory based on what's adjacent.
            if (tagReserveMemoryHeuristically)
                CollapseReserveRegions(ranges);

            if (printAllMemory)
            {
                int kindSize = ranges.Max(r => r.Kind.ToString().Length);
                int stateSize = ranges.Max(r => r.State.ToString().Length);
                int protectSize = ranges.Max(r => r.Protect.ToString().Length);

                TableOutput output = new((nameSizeMax, ""), (12, "x"), (12, "x"), (12, ""), (kindSize, ""), (stateSize, ""), (protectSize, ""))
                {
                    AlignLeft = true,
                    Divider = " | "
                };

                output.WriteRowWithSpacing('-', "Memory Kind", "StartAddr", "EndAddr-1", "Size", "Type", "State", "Protect", "Image");
                foreach (AddressMemoryRange mem in ranges)
                    output.WriteRow(mem.Name, mem.Start, mem.End, mem.Length.ConvertToHumanReadable(), mem.Kind, mem.State, mem.Protect, mem.Image);

                output.WriteSpacer('-');
            }

            if (showImageTable)
            {
                var imageGroups = from mem in ranges.Where(r => r.State != MemState.MEM_RESERVE && r.Image != null)
                                  group mem by mem.Image into g
                                  let Size = g.Sum(k => (long)(k.End - k.Start))
                                  orderby Size descending
                                  select new {
                                      Image = g.Key,
                                      Count = g.Count(),
                                      Size
                                  };

                int moduleLen = Math.Max(80, ranges.Max(r => r.Image?.Length ?? 0));

                TableOutput output = new((moduleLen, ""), (8, "n0"), (12, ""), (24, "n0"))
                {
                    Divider = " | "
                };

                output.WriteRowWithSpacing('-', "Image", "Regions", "Size", "Size (bytes)");

                int count = 0;
                long size = 0;
                foreach (var item in imageGroups)
                {
                    output.WriteRow(item.Image, item.Count, item.Size.ConvertToHumanReadable(), item.Size);
                    count += item.Count;
                    size += item.Size;
                }

                output.WriteSpacer('-');
                output.WriteRow("[TOTAL]", count, size.ConvertToHumanReadable(), size);
                Console.WriteLine();
            }


            // Print summary table unconditionally
            {
                var grouped = from mem in ranges
                              let name = mem.Name
                              group mem by name into g
                              let Count = g.Count()
                              let Size = g.Sum(f => (long)(f.End - f.Start))
                              orderby Size descending
                              select new {
                                  Name = g.Key,
                                  Count,
                                  Size
                              };

                TableOutput output = new((-nameSizeMax, ""), (8, "n0"), (12, ""), (24, "n0"))
                {
                    Divider = " | "
                };

                output.WriteRowWithSpacing('-', "Region Type", "Count", "Size", "Size (bytes)");

                int count = 0;
                long size = 0;
                foreach (var item in grouped)
                {
                    output.WriteRow(item.Name, item.Count, item.Size.ConvertToHumanReadable(), item.Size);
                    count += item.Count;
                    size += item.Size;
                }

                output.WriteSpacer('-');
                output.WriteRow("[TOTAL]", count, size.ConvertToHumanReadable(), size);
            }
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
        /// <exception cref="InvalidOperationException">If !address fails we will throw InvalidOperationException.  This is usually
        /// because symbols for ntdll couldn't be found.</exception>
        /// <returns>An enumerable of memory ranges.</returns>
        public IEnumerable<AddressMemoryRange> EnumerateAddressSpace(bool tagClrMemoryRanges, bool includeReserveMemory, bool tagReserveMemoryHeuristically)
        {
            IEnumerable<AddressMemoryRange> addressResult = EnumerateBangAddress().Where(m => m.State != MemState.MEM_FREE);

            // TODO: remove this, it's code that's just for a particular dump I'm using to test this with
            addressResult = addressResult.Where(m => (m.Start & 0xffffffff00000000) != 0xffffffff00000000 && (m.End & 0xffffffff00000000) != 0xffffffff00000000);

            if (!includeReserveMemory)
                addressResult = addressResult.Where(m => m.State != MemState.MEM_RESERVE);

            AddressMemoryRange[] ranges = addressResult.OrderBy(r => r.Start).ToArray();
            if (tagClrMemoryRanges)
            {
                foreach (ClrMemoryPointer mem in EnumerateClrMemoryAddresses())
                {
                    AddressMemoryRange[] found = ranges.Where(m => m.Start <= mem.Address && mem.Address < m.End).ToArray();

                    if (found.Length == 0)
                        Trace.WriteLine($"Warning:  Could not find a memory range for {mem.Address:x} - {mem.Kind}.");
                    else if (found.Length > 1)
                        Trace.WriteLine($"Warning:  Found multiple memory ranges for entry {mem.Address:x} - {mem.Kind}.");

                    foreach (AddressMemoryRange? entry in found)
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
                    string memName = mem.Name;
                    if (memName == "RESERVED")
                        TagMemoryRecursive(mem, ranges);
                }
            }

            // On Linux, !address doesn't mark stack space.  Go do that.
            if (DataTarget.DataReader.TargetPlatform == OSPlatform.Linux)
                MarkStackSpace(ranges);

            return ranges;
        }

        private void MarkStackSpace(AddressMemoryRange[] ranges)
        {
            IThreadReader? threadReader = DataTarget.DataReader as IThreadReader;
            Architecture arch = DataTarget.DataReader.Architecture;
            int size = arch switch
            {
                Architecture.Arm => ArmContext.Size,
                Architecture.Arm64 => Arm64Context.Size,
                (Architecture)9 /* Architecture.RiscV64 */ => RiscV64Context.Size,
                Architecture.X86 => X86Context.Size,
                Architecture.X64 => AMD64Context.Size,
                _ => 0
            };

            if (size > 0 && threadReader is not null)
            {
                byte[] rawBuffer = ArrayPool<byte>.Shared.Rent(size);
                try
                {
                    Span<byte> buffer = rawBuffer.AsSpan()[0..size];

                    foreach (uint thread in threadReader.EnumerateOSThreadIds())
                    {
                        ulong sp = GetStackPointer(arch, buffer, thread);

                        AddressMemoryRange? range = FindMemory(ranges, sp);
                        if (range is not null)
                            range.Description = "Stack";
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rawBuffer);
                }
            }
        }

        private unsafe ulong GetStackPointer(Architecture arch, Span<byte> buffer, uint thread)
        {
            ulong sp = 0;

            bool res = DataTarget.DataReader.GetThreadContext(thread, 0, buffer);
            if (res)
            {
                switch (arch)
                {
                    case Architecture.X86:
                        fixed (byte* ptrCtx = buffer)
                        {
                            X86Context* ctx = (X86Context*)ptrCtx;
                            sp = ctx->Esp;
                        }
                        break;

                    case Architecture.X64:
                        fixed (byte* ptrCtx = buffer)
                        {
                            AMD64Context* ctx = (AMD64Context*)ptrCtx;
                            sp = ctx->Rsp;
                        }
                        break;

                    case Architecture.Arm64:
                        fixed (byte* ptrCtx = buffer)
                        {
                            Arm64Context* ctx = (Arm64Context*)ptrCtx;
                            sp = ctx->Sp;
                        }
                        break;

                    case Architecture.Arm:
                        fixed (byte* ptrCtx = buffer)
                        {
                            ArmContext* ctx = (ArmContext*)ptrCtx;
                            sp = ctx->Sp;
                        }
                        break;

                    case (Architecture)9 /* Architecture.RiscV64 */:
                        fixed (byte* ptrCtx = buffer)
                        {
                            RiscV64Context* ctx = (RiscV64Context*)ptrCtx;
                            sp = ctx->Sp;
                        }
                        break;

                    case Architecture.LoongArch64:
                        fixed (byte* ptrCtx = buffer)
                        {
                            LoongArch64Context* ctx = (LoongArch64Context*)ptrCtx;
                            sp = ctx->Sp;
                        }
                        break;

                }
            }

            return sp;
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
        public static void CollapseReserveRegions(AddressMemoryRange[] ranges)
        {
            foreach (AddressMemoryRange mem in ranges)
            {
                string memName = mem.Name;
                if (memName == "RESERVED")
                    TagMemoryRecursive(mem, ranges);
            }
        }

        /// <summary>
        /// Enumerates pointers to various CLR heaps in memory.
        /// </summary>
        public IEnumerable<ClrMemoryPointer> EnumerateClrMemoryAddresses()
        {
            foreach (ClrRuntime runtime in Runtimes)
            {
                SOSDac sos = runtime.DacLibrary.SOSDacInterface;
                foreach (JitManagerInfo jitMgr in sos.GetJitManagers())
                {
                    foreach (ClrHandle handle in runtime.EnumerateHandles())
                        yield return new ClrMemoryPointer() { Kind = ClrMemoryKind.HandleTable, Address = handle.Address };

                    foreach (JitCodeHeapInfo mem in sos.GetCodeHeapList(jitMgr.Address))
                        yield return new ClrMemoryPointer()
                        {
                            Address = mem.Address,
                            Kind = mem.Kind switch
                            {
                                CodeHeapKind.Loader => ClrMemoryKind.LoaderHeap,
                                CodeHeapKind.Host => ClrMemoryKind.Host,
                                _ => ClrMemoryKind.UnknownCodeHeap
                            }
                        };

                    foreach (ClrSegment seg in runtime.Heap.Segments)
                    {
                        if (seg.CommittedMemory.Length > 0)
                            yield return new ClrMemoryPointer() { Address = seg.CommittedMemory.Start, Kind = ClrMemoryKind.GCHeapSegment };

                        if (seg.ReservedMemory.Length > 0)
                            yield return new ClrMemoryPointer() { Address = seg.ReservedMemory.Start, Kind = ClrMemoryKind.GCHeapReserve };
                    }

                    HashSet<ulong> seen = new();

                    List<ClrMemoryPointer> heaps = new();
                    if (runtime.SystemDomain is not null)
                        AddAppDomainHeaps(sos, runtime.SystemDomain.Address, heaps);

                    if (runtime.SharedDomain is not null)
                        AddAppDomainHeaps(sos, runtime.SharedDomain.Address, heaps);

                    foreach (ClrMemoryPointer heap in heaps)
                        if (seen.Add(heap.Address))
                            yield return heap;

                    foreach (ClrDataAddress address in sos.GetAppDomainList())
                    {
                        heaps.Clear();
                        AddAppDomainHeaps(sos, address, heaps);

                        foreach (ClrMemoryPointer heap in heaps)
                            if (seen.Add(heap.Address))
                                yield return heap;
                    }
                }
            }
        }

        private static void AddAppDomainHeaps(SOSDac sos, ClrDataAddress address, List<ClrMemoryPointer> heaps)
        {
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


                sos.TraverseStubHeap(address, VCSHeapType.LookupHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                {
                    Address = address,
                    Kind = ClrMemoryKind.LookupHeap
                }));


                sos.TraverseStubHeap(address, VCSHeapType.ResolveHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                {
                    Address = address,
                    Kind = ClrMemoryKind.ResolveHeap
                }));


                sos.TraverseStubHeap(address, VCSHeapType.DispatchHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                {
                    Address = address,
                    Kind = ClrMemoryKind.DispatchHeap
                }));

                sos.TraverseStubHeap(address, VCSHeapType.CacheEntryHeap, (address, size, isCurrent) => heaps.Add(new ClrMemoryPointer()
                {
                    Address = address,
                    Kind = ClrMemoryKind.CacheEntryHeap
                }));
            }
        }

        /// <summary>
        /// Enumerates the output of !address in a usable format.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">If !address fails we will throw InvalidOperationException.  This is usually
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

                    // On Linux, !address is reporting this as MEM_PRIVATE or MEM_UNKNOWN
                    if (description == "Image")
                        kind = MemKind.MEM_IMAGE;

                    // On Linux, !address is reporting this as nothing
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
            if (mem.Name != "RESERVED")
                return mem;

            AddressMemoryRange? found = ranges.SingleOrDefault(r => r.End == mem.Start);
            if (found is null)
                return null;

            AddressMemoryRange? nonReserved = TagMemoryRecursive(found, ranges);
            if (nonReserved is null)
                return null;

            mem.Description = nonReserved.Name;
            return nonReserved;
        }

        internal static ulong FindMostCommonPointer(IEnumerable<ulong> enumerable)
            => (from ptr in enumerable
                group ptr by ptr into g
                orderby g.Count() descending
                select g.First()).First();

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
            HandleTable,
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

            public string Name
            {
                get
                {
                    if (ClrMemoryKind != ClrMemoryKind.None)
                        return ClrMemoryKind.ToString();

                    if (!string.IsNullOrWhiteSpace(Description))
                        return Description;

                    if (State == MemState.MEM_RESERVE)
                        return "RESERVED";
                    else if (State == MemState.MEM_FREE)
                        return "FREE";

                    string result = Protect.ToString();
                    if (Kind == MemKind.MEM_MAPPED)
                    {
                        if (string.IsNullOrWhiteSpace(result))
                            result = Kind.ToString();
                        else
                            result = result.Replace("PAGE", "MAPPED");
                    }

                    return result;
                }
            }

            public override string ToString()
            {
                return $"[{Start:x}-{End:x}] {Name}";
            }
        }

        public class ClrMemoryPointer
        {
            public ulong Address { get; internal set; }
            public ClrMemoryKind Kind { get; internal set; }
        }
    }
}