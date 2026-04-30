// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Windows;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class MinidumpReader : IDataReader, IDisposable, IThreadReader, IDumpInfoProvider, IDumpFileMemorySource, IThreadInfoReader, IProcessInfoProvider, IMemoryRegionReader
    {
        private readonly Minidump _minidump;
        private readonly DataTargetLimits? _limits;
        private IMemoryReader? _readerCached;

        public OSPlatform TargetPlatform => OSPlatform.Windows;

        public string DisplayName { get; }

        public IMemoryReader MemoryReader => _readerCached ??= _minidump.MemoryReader;

        public MinidumpReader(string displayName, Stream stream, CacheOptions cacheOptions, bool leaveOpen, DataTargetLimits? limits = null)
        {
            if (displayName is null)
                throw new ArgumentNullException(nameof(displayName));

            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            DisplayName = displayName;

            _limits = limits;
            _minidump = new Minidump(displayName, stream, cacheOptions, leaveOpen, limits);

            Architecture = _minidump.Architecture switch
            {
                MinidumpProcessorArchitecture.Amd64 => Architecture.X64,
                MinidumpProcessorArchitecture.Arm => Architecture.Arm,
                MinidumpProcessorArchitecture.Arm64 => Architecture.Arm64,
                MinidumpProcessorArchitecture.Intel => Architecture.X86,
                _ => throw new NotSupportedException($"Architecture {_minidump.Architecture} is not supported."),
            };

            PointerSize = _minidump.PointerSize;
        }

        public bool IsThreadSafe => true;

        public Architecture Architecture { get; }

        public int ProcessId => _minidump.ProcessId;

        public int PointerSize { get; }

        public bool IsMiniOrTriage => _minidump.IsMiniDump;

        public bool IsCreatedByDotNetRuntime => true;

        public void Dispose()
        {
            _minidump.Dispose();
        }

        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            // We set buildId to "Empty" since only PEImages exist where minidumps are created, and we do not
            // want to try to lazily evaluate the buildId later
            return from module in _minidump.EnumerateModuleInfo()
                   select new PEModuleInfo(this, module.BaseOfImage, module.ModuleName ?? "", true, module.DateTimeStamp, module.SizeOfImage, limits: _limits);
        }

        public void FlushCachedData()
        {
        }

        public IEnumerable<uint> EnumerateOSThreadIds() => _minidump.OrderedThreads;

        public ulong GetThreadTeb(uint osThreadId)
        {
            _minidump.Tebs.TryGetValue(osThreadId, out ulong teb);
            return teb;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            int index = _minidump.ContextData.Search(threadID, (x, y) => x.ThreadId.CompareTo(y));
            if (index < 0)
                return false;

            MinidumpContextData ctx = _minidump.ContextData[index];
            if (ctx.ContextRva == 0 || ctx.ContextBytes == 0)
                return false;

            return _minidump.MemoryReader.ReadFromRva(ctx.ContextRva, context) == context.Length;
        }

        public int Read(ulong address, Span<byte> buffer) => MemoryReader.Read(address, buffer);
        public bool Read<T>(ulong address, out T value) where T : unmanaged => MemoryReader.Read(address, out value);
        public T Read<T>(ulong address) where T : unmanaged => MemoryReader.Read<T>(address);
        public bool ReadPointer(ulong address, out ulong value) => MemoryReader.ReadPointer(address, out value);
        public ulong ReadPointer(ulong address) => MemoryReader.ReadPointer(address);

        public IReadOnlyList<DumpMemorySegment> EnumerateMemorySegments()
        {
            ImmutableArray<MinidumpSegment> segments = _minidump.Segments;
            DumpMemorySegment[] result = new DumpMemorySegment[segments.Length];
            for (int i = 0; i < segments.Length; i++)
                result[i] = new DumpMemorySegment(segments[i].VirtualAddress, segments[i].FileOffset, segments[i].Size);
            return result;
        }

        // -- IThreadInfoReader --

        public bool TryGetThreadInfo(uint osThreadId, out ThreadInfo info)
        {
            bool hasInfo = _minidump.ThreadInfos.ContainsKey(osThreadId);
            bool hasState = _minidump.ThreadStates.ContainsKey(osThreadId);
            if (!hasInfo && !hasState)
            {
                info = default;
                return false;
            }

            info = BuildThreadInfo(osThreadId);
            return true;
        }

        public IEnumerable<ThreadInfo> EnumerateThreadInfo()
        {
            foreach (uint tid in _minidump.OrderedThreads)
                yield return BuildThreadInfo(tid);
        }

        private ThreadInfo BuildThreadInfo(uint osThreadId)
        {
            bool hasInfo = _minidump.ThreadInfos.TryGetValue(osThreadId, out MinidumpThreadInfo info);
            bool hasState = _minidump.ThreadStates.TryGetValue(osThreadId, out MinidumpThread state);

            if (!hasInfo && !hasState)
                return default;

            DateTime? createTime = hasInfo && info.CreateTime != 0 ? FileTimeToDateTime(info.CreateTime) : null;
            DateTime? exitTime = hasInfo && info.ExitTime != 0 ? FileTimeToDateTime(info.ExitTime) : null;
            TimeSpan? userTime = hasInfo ? FileTimeIntervalToTimeSpan(info.UserTime) : null;
            TimeSpan? kernelTime = hasInfo ? FileTimeIntervalToTimeSpan(info.KernelTime) : null;
            return new ThreadInfo
            {
                OSThreadId = osThreadId,
                CreateTimeUtc = createTime,
                ExitTimeUtc = exitTime,
                UserTime = userTime,
                KernelTime = kernelTime,
                StartAddress = hasInfo ? info.StartAddress : 0,
                Affinity = hasInfo ? info.Affinity : 0,
                SuspendCount = hasState ? state.SuspendCount : 0,
                PriorityClass = hasState ? state.PriorityClass : 0,
                Priority = hasState ? state.Priority : 0,
                StackBase = hasState ? state.Stack.StartAddress + state.Stack.DataSize32 : 0,
                StackSize = hasState ? state.Stack.DataSize32 : 0,
                ExitStatus = hasInfo ? info.ExitStatus : 0,
                DumpFlags = hasInfo ? info.DumpFlags : 0,
            };
        }

        private static DateTime? FileTimeToDateTime(ulong fileTime)
        {
            // FILETIME is 100-ns intervals since 1601-01-01 UTC.
            if (fileTime > long.MaxValue)
                return null;

            try
            {
                return DateTime.FromFileTimeUtc((long)fileTime);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static TimeSpan? FileTimeIntervalToTimeSpan(ulong fileTimeInterval)
        {
            // 100-ns ticks ≡ TimeSpan ticks.
            if (fileTimeInterval > long.MaxValue)
                return null;

            return TimeSpan.FromTicks((long)fileTimeInterval);
        }

        // -- IProcessInfoProvider --

        public ProcessInfo GetProcessInfo()
        {
            string? imagePath = _minidump.EnumerateModuleInfo().FirstOrDefault()?.ModuleName;

            DateTime? createTime = null;
            TimeSpan? userTime = null;
            TimeSpan? kernelTime = null;

            if (_minidump.MiscInfo is MinidumpMiscInfo misc
                && (misc.Flags1 & MinidumpMiscInfo.MINIDUMP_MISC1_PROCESS_TIMES) != 0)
            {
                createTime = misc.ProcessCreateTime != 0
                    ? DateTimeOffset.FromUnixTimeSeconds(misc.ProcessCreateTime).UtcDateTime
                    : (DateTime?)null;
                userTime = TimeSpan.FromSeconds(misc.ProcessUserTime);
                kernelTime = TimeSpan.FromSeconds(misc.ProcessKernelTime);
            }

            Version? osVersion = null;
            if (_minidump.SystemInfo.MajorVersion != 0 || _minidump.SystemInfo.MinorVersion != 0 || _minidump.SystemInfo.BuildNumber != 0)
            {
                osVersion = new Version(
                    (int)_minidump.SystemInfo.MajorVersion,
                    (int)_minidump.SystemInfo.MinorVersion,
                    (int)_minidump.SystemInfo.BuildNumber);
            }

            string? osBuild = _minidump.BuildString;
            if (string.IsNullOrEmpty(osBuild))
                osBuild = string.IsNullOrEmpty(_minidump.CSDVersion) ? null : _minidump.CSDVersion;

            return new ProcessInfo
            {
                ProcessId = (uint)_minidump.ProcessId,
                ImagePath = imagePath,
                CommandLine = null,
                CreateTimeUtc = createTime,
                UserTime = userTime,
                KernelTime = kernelTime,
                Architecture = Architecture,
                TargetPlatform = TargetPlatform,
                OSVersion = osVersion,
                OSBuildString = osBuild,
                ProcessorCount = _minidump.SystemInfo.NumberOfProcessors,
                DumpTimestampUtc = _minidump.DumpTimestampUtc,
            };
        }

        // -- IMemoryRegionReader --

        public IEnumerable<MemoryRegion> EnumerateMemoryRegions()
        {
            ImmutableArray<MinidumpMemoryInfo> regions = _minidump.MemoryInfoRegions;
            if (!regions.IsDefaultOrEmpty)
            {
                foreach (MinidumpMemoryInfo r in regions)
                {
                    yield return new MemoryRegion
                    {
                        BaseAddress = r.BaseAddress,
                        Size = r.RegionSize,
                        State = MapState(r.State),
                        Type = MapType(r.Type),
                        Protect = MapProtect(r.Protect),
                        AllocationBase = r.AllocationBase,
                        AllocationProtect = MapProtect(r.AllocationProtect),
                    };
                }

                yield break;
            }

            // Fallback: synthesize commit/read regions from the dump's saved-memory list.
            foreach (MinidumpSegment seg in _minidump.Segments)
            {
                yield return new MemoryRegion
                {
                    BaseAddress = seg.VirtualAddress,
                    Size = seg.Size,
                    State = MemoryRegionState.Commit,
                    Type = MemoryRegionType.Unknown,
                    Protect = MemoryRegionProtect.Read,
                    AllocationBase = seg.VirtualAddress,
                    AllocationProtect = MemoryRegionProtect.Read,
                };
            }
        }

        private static MemoryRegionState MapState(uint state) => state switch
        {
            MinidumpMemoryInfo.MEM_COMMIT => MemoryRegionState.Commit,
            MinidumpMemoryInfo.MEM_RESERVE => MemoryRegionState.Reserve,
            MinidumpMemoryInfo.MEM_FREE => MemoryRegionState.Free,
            _ => MemoryRegionState.Unknown,
        };

        private static MemoryRegionType MapType(uint type) => type switch
        {
            MinidumpMemoryInfo.MEM_PRIVATE => MemoryRegionType.Private,
            MinidumpMemoryInfo.MEM_MAPPED => MemoryRegionType.Mapped,
            MinidumpMemoryInfo.MEM_IMAGE => MemoryRegionType.Image,
            _ => MemoryRegionType.Unknown,
        };

        private static MemoryRegionProtect MapProtect(uint protect)
        {
            if (protect == 0)
                return MemoryRegionProtect.None;

            MemoryRegionProtect result = MemoryRegionProtect.None;

            // Base RWX classification — exactly one of these bits is meaningful at a time on Windows.
            switch (protect & 0xFF)
            {
                case MinidumpMemoryInfo.PAGE_NOACCESS:          result = MemoryRegionProtect.NoAccess; break;
                case MinidumpMemoryInfo.PAGE_READONLY:          result = MemoryRegionProtect.Read; break;
                case MinidumpMemoryInfo.PAGE_READWRITE:         result = MemoryRegionProtect.Read | MemoryRegionProtect.Write; break;
                case MinidumpMemoryInfo.PAGE_WRITECOPY:         result = MemoryRegionProtect.Read | MemoryRegionProtect.WriteCopy; break;
                case MinidumpMemoryInfo.PAGE_EXECUTE:           result = MemoryRegionProtect.Execute; break;
                case MinidumpMemoryInfo.PAGE_EXECUTE_READ:      result = MemoryRegionProtect.Execute | MemoryRegionProtect.Read; break;
                case MinidumpMemoryInfo.PAGE_EXECUTE_READWRITE: result = MemoryRegionProtect.Execute | MemoryRegionProtect.Read | MemoryRegionProtect.Write; break;
                case MinidumpMemoryInfo.PAGE_EXECUTE_WRITECOPY: result = MemoryRegionProtect.Execute | MemoryRegionProtect.Read | MemoryRegionProtect.WriteCopy; break;
            }

            if ((protect & MinidumpMemoryInfo.PAGE_GUARD) != 0)
                result |= MemoryRegionProtect.Guard;
            if ((protect & MinidumpMemoryInfo.PAGE_NOCACHE) != 0)
                result |= MemoryRegionProtect.NoCache;
            if ((protect & MinidumpMemoryInfo.PAGE_WRITECOMBINE) != 0)
                result |= MemoryRegionProtect.WriteCombine;

            return result;
        }
    }
}