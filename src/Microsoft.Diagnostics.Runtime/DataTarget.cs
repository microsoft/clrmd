﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Azure.Core;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Interfaces;
using Microsoft.Diagnostics.Runtime.MacOS;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A crash dump or live process to read out of.
    /// </summary>
    public sealed class DataTarget : IDisposable, IDataTarget
    {
        private static readonly List<IClrInfoProvider> s_clrInfoProviders = new() { new DotNetClrInfoProvider(), new SingleFileClrInfoProvider() };

        private bool _disposed;
        private ImmutableArray<ClrInfo> _clrs;
        private ModuleInfo[]? _modules;
        private readonly Dictionary<string, PEImage?> _pefileCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Adds an IClrInfoProvider to DataTarget.  There are no guarantees on order in which they are called.
        /// </summary>
        /// <param name="clrInfoProvider">The provider to add.</param>
        public static void AddClrInfoProvider(IClrInfoProvider clrInfoProvider)
        {
            s_clrInfoProviders.Add(clrInfoProvider);
        }

        /// <summary>
        /// Custom data target for this data target
        /// </summary>
        internal CustomDataTarget CustomDataTarget { get; }

        /// <summary>
        /// Gets the data reader for this instance.
        /// </summary>
        public IDataReader DataReader { get; }

        /// <summary>
        /// The caching options for ClrMD.  This controls what kinds of memory we cache and what values have to be
        /// recalculated on every call.
        /// </summary>
        public CacheOptions CacheOptions { get; }

        /// <summary>
        /// Gets or sets instance to manage the symbol path(s).
        /// </summary>
        public IFileLocator? FileLocator { get => CustomDataTarget.FileLocator; set => CustomDataTarget.FileLocator = value; }

        /// <summary>
        /// Creates a DataTarget from the given reader.
        /// </summary>
        /// <param name="customTarget">The custom data target to use.</param>
        public DataTarget(CustomDataTarget customTarget)
        {
            CustomDataTarget = customTarget ?? throw new ArgumentNullException(nameof(customTarget));
            DataReader = CustomDataTarget.DataReader;
            CacheOptions = CustomDataTarget.CacheOptions ?? new CacheOptions();

            IFileLocator? locator = CustomDataTarget.FileLocator;
            if (locator == null)
            {
                string sympath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH") ?? "";
                bool symTrace = CustomDataTarget.GetTraceEnvironmentVariable();
                locator = SymbolGroup.CreateFromSymbolPath(sympath, trace:symTrace, CustomDataTarget.SymbolTokenCredential);
            }

            FileLocator = locator;
        }

        public void SetSymbolPath(string symbolPath)
        {
            if (symbolPath is null)
                throw new ArgumentNullException(nameof(symbolPath));

            bool symTrace = CustomDataTarget.GetTraceEnvironmentVariable();
            FileLocator = SymbolGroup.CreateFromSymbolPath(symbolPath, trace:symTrace, CustomDataTarget.SymbolTokenCredential);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_pefileCache)
                {
                    foreach (PEImage? img in _pefileCache.Values)
                        img?.Dispose();

                    _pefileCache.Clear();
                }

                CustomDataTarget.Dispose();
                _disposed = true;
            }
        }

        internal PEImage? LoadPEImage(string fileName, int timeStamp, int fileSize, bool checkProperties, ulong imageBase)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DataTarget));

            if (string.IsNullOrEmpty(fileName))
                return null;

            string key = $"{fileName}/{timeStamp:x}{fileSize:x}";

            PEImage? result = null;

            lock (_pefileCache)
            {
                if (_pefileCache.TryGetValue(key, out result))
                    return result;
            }

            if (FileLocator is not null)
            {
                string? foundFile = FileLocator.FindPEImage(fileName, timeStamp, fileSize, checkProperties);
                if (!string.IsNullOrWhiteSpace(foundFile) && File.Exists(foundFile))
                {
                    try
                    {
                        result = new PEImage(File.OpenRead(foundFile), false, imageBase);
                        if (!result.IsValid)
                            result = null;
                    }
                    catch (IOException)
                    {
                        result = null;
                    }
                }
            }

            if (result is null)
            {
                // If we have a custom file locator (or null), we might not have checked the file on disk
                if (Path.GetFileName(fileName) != fileName && File.Exists(fileName))
                {
                    try
                    {
                        result = new(File.OpenRead(fileName), leaveOpen: false);
                        if (!result.IsValid)
                        {
                            result = null;
                        }
                        else if (checkProperties)
                        {
                            if (result.IndexFileSize != fileSize || result.IndexTimeStamp != timeStamp)
                                result = null;
                        }
                    }
                    catch (IOException)
                    {
                        result = null;
                    }
                }
            }

            lock (_pefileCache)
            {
                // We may have raced with another thread and that thread put a value here first
                if (_pefileCache.TryGetValue(key, out PEImage? cached) && cached != null)
                {
                    result?.Dispose(); // We don't need this instance now.
                    return cached;
                }

                return _pefileCache[key] = result;
            }
        }

        [Conditional("DEBUG")]
        private void DebugOnlyLoadLazyValues()
        {
            // Prefetch these values in debug builds for easier debugging
            GetOrCreateClrVersions();
            EnumerateModules();
        }

        /// <summary>
        /// Gets the list of CLR versions loaded into the process.
        /// </summary>
        public ImmutableArray<ClrInfo> ClrVersions => GetOrCreateClrVersions();
        ImmutableArray<IClrInfo> IDataTarget.ClrVersions => ClrVersions.CastArray<IClrInfo>();

        private ImmutableArray<ClrInfo> GetOrCreateClrVersions()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DataTarget));

            if (_clrs.IsDefault)
            {
                IEnumerable<ModuleInfo> modules = EnumerateModules();
                List<ClrInfo>? clrs = null;

                // First try the SpecialDiagInfo block short cut to find a supported runtime
                if (!CustomDataTarget.ForceCompleteRuntimeEnumeration && DataReader.TargetPlatform != OSPlatform.Windows)
                {
                    if (SpecialDiagInfo.TryReadSpecialDiagInfo(DataReader, out SpecialDiagInfo info) && info.RuntimeBaseAddress != 0)
                    {
                        Debug.Assert(_modules != null);
                        
                        // The above EnumerateModules() call populated (the sorted) _modules array
                        int found = _modules!.Search(info.RuntimeBaseAddress, (m, k) => m.ImageBase.CompareTo(k));
                        if (found > 0)
                        {
                            // Run the module through the clr info providers to see if it is an app model/runtime supported by CLRMD
                            clrs = new(
                                from clrInfo in s_clrInfoProviders.Select(provider => provider.ProvideClrInfoForModule(this, _modules![found])).Where(clrInfo => clrInfo != null)
                                orderby clrInfo.Flavor descending, clrInfo.Version
                                select clrInfo);
                        }
                    }
                }

                // If no runtimes where found from the SpecialDiagInfo block, run all the modules through the clr info providers

                // We order this so .Net Core comes first, so if there's multiple CLRs we prefer
                // to debug .Net Core (assuming the user is just debugging one of them)

                if (clrs == null || clrs.Count == 0)
                {
                    clrs = new(
                        from module in modules
                        from clrInfo in s_clrInfoProviders.Select(provider => provider.ProvideClrInfoForModule(this, module)).Where(clrInfo => clrInfo != null)
                        orderby clrInfo.Flavor descending, clrInfo.Version
                        select clrInfo);
                }

                _clrs = clrs.ToImmutableArray();
            }

            return _clrs;
        }

        /// <summary>
        /// Enumerates information about the loaded modules in the process (both managed and unmanaged).
        /// </summary>
        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DataTarget));

            if (_modules != null)
                return _modules;

            char[] invalid = Path.GetInvalidPathChars();
            ModuleInfo[] modules = DataReader.EnumerateModules().Where(m => m.FileName != null && m.FileName.IndexOfAny(invalid) < 0).ToArray();
            Array.Sort(modules, (a, b) => a.ImageBase.CompareTo(b.ImageBase));

            return _modules = modules;
        }

        /// <summary>
        /// Gets a set of helper functions that are consistently implemented across all platforms.
        /// </summary>
        public static PlatformFunctions PlatformFunctions { get; } =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? new LinuxFunctions() :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? new MacOSFunctions() :
            new WindowsFunctions();

        /// <summary>
        /// Loads a dump stream. Currently supported formats are Mach-O coredump, ELF coredump, and Windows Minidump formats.
        /// </summary>
        /// <param name="displayName">The name of this DataTarget, might be used in exceptions.</param>
        /// <param name="stream">The stream that should be used.</param>
        /// <param name="cacheOptions">The caching options to use. (Only used for FileStreams)</param>
        /// <param name="leaveOpen">True whenever the given stream should be leaved open when the DataTarget is disposed.</param>
        /// <param name="symbolCredential">A TokenCredential for azure based symbol servers.</param>
        /// <returns>A <see cref="DataTarget"/> for the given dump.</returns>
        public static DataTarget LoadDump(string displayName, Stream stream, CacheOptions? cacheOptions = null, bool leaveOpen = false, TokenCredential? symbolCredential = null)
        {
            try
            {
                if (displayName is null)
                    throw new ArgumentNullException(nameof(displayName));
                if (stream is null)
                    throw new ArgumentNullException(nameof(stream));
                if (stream.Position != 0)
                    throw new ArgumentException("Stream must be at position 0", nameof(stream));
                if (!stream.CanSeek)
                    throw new ArgumentException("Stream must be seekable", nameof(stream));
                if (!stream.CanRead)
                    throw new ArgumentException("Stream must be readable", nameof(stream));

                cacheOptions ??= new CacheOptions();

                DumpFileFormat format = ReadFileFormat(stream);
                IDataReader reader = format switch
                {
                    DumpFileFormat.Minidump => new MinidumpReader(displayName, stream, cacheOptions, leaveOpen),
                    DumpFileFormat.ElfCoredump => new CoredumpReader(displayName, stream, leaveOpen),
                    DumpFileFormat.MachOCoredump => new MachOCoreReader(displayName, stream, leaveOpen),

                    // USERDU64 dumps are the "old" style of dumpfile.  This file format is very old and shouldn't be
                    // used.  However, IDebugClient::WriteDumpFile(,DEBUG_DUMP_DEFAULT) still generates this format
                    // (at least with the Win10 system32\dbgeng.dll), so we will support this for now.
                    DumpFileFormat.Userdump64 => throw new NotSupportedException($"This dump is in the Userdump64 format, which is not supported by ClrMD directly. " +
                                "DbgEng can read this dump format, which can be obtained via DbgEngDataReader in the Microsoft.Diagnostics.Runtime.Utilities NuGet package."),

                    DumpFileFormat.CompressedArchive => throw new InvalidDataException($"Stream '{displayName}' is a compressed archived instead of a dump file."),
                    _ => throw new InvalidDataException($"Stream '{displayName}' is in an unknown or unsupported file format."),
                };

                return new DataTarget(new CustomDataTarget(reader, symbolCredential) { CacheOptions = cacheOptions });
            }
            catch
            {
                if (!leaveOpen)
                    stream?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Loads a dump file. Currently supported formats are Mach-O coredump, ELF coredump, and Windows Minidump formats.
        /// </summary>
        /// <param name="filePath">The path to the dump file.</param>
        /// <param name="symbolCredential">The TokenCredential to use for any Azure based symbol servers (set to null if not using one).</param>
        /// <returns>A <see cref="DataTarget"/> for the given dump file.</returns>
        public static DataTarget LoadDump(string filePath, TokenCredential? symbolCredential = null) => LoadDump(filePath, null, symbolCredential);

        /// <summary>
        /// Loads a dump file. Currently supported formats are Mach-O coredump, ELF coredump, and Windows Minidump formats.
        /// </summary>
        /// <param name="filePath">The path to the dump file.</param>
        /// <param name="cacheOptions">The caching options to use.</param>
        /// <param name="symbolCredential">The TokenCredential to use for any Azure based symbol servers (set to null if not using one).</param>
        /// <returns>A <see cref="DataTarget"/> for the given dump file.</returns>
        public static DataTarget LoadDump(string filePath, CacheOptions? cacheOptions, TokenCredential? symbolCredential = null)
        {
            if (filePath is null)
                throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Could not open dump file '{filePath}'.", filePath);
            FileStream stream = File.OpenRead(filePath);
            return LoadDump(filePath, stream, cacheOptions, leaveOpen: false, symbolCredential: symbolCredential);
        }

        private static DumpFileFormat ReadFileFormat(Stream stream)
        {
            Span<byte> span = stackalloc byte[8];
            int readCount = stream.Read(span);
            stream.Position -= readCount; // Reset stream position

            if (readCount != span.Length)
                throw new InvalidDataException("Unable to load the header.");

            uint first = Unsafe.As<byte, uint>(ref span[0]);
            DumpFileFormat format = first switch
            {
                0x504D444D => DumpFileFormat.Minidump,          // MDMP
                0x464c457f => DumpFileFormat.ElfCoredump,       // ELF
                0x52455355 => DumpFileFormat.Userdump64,        // USERDU64
                0x4643534D => DumpFileFormat.CompressedArchive, // CAB
                0xfeedfacf => DumpFileFormat.MachOCoredump,
                0xfeedface => DumpFileFormat.MachOCoredump,
                _ => DumpFileFormat.Unknown,
            };

            if (format == DumpFileFormat.Unknown)
            {
                if (span[0] == 'B' && span[1] == 'Z')           // BZip2
                    format = DumpFileFormat.CompressedArchive;
                else if (span[0] == 0x1f && span[1] == 0x8b)    // GZip
                    format = DumpFileFormat.CompressedArchive;
                else if (span[0] == 0x50 && span[1] == 0x4b)    // Zip
                    format = DumpFileFormat.CompressedArchive;
            }

            return format;
        }

        /// <summary>
        /// Attaches to a running process.  Note that if <paramref name="suspend"/> is set to false the user
        /// of ClrMD is still responsible for suspending the process itself.  ClrMD does NOT support inspecting
        /// a running process and will produce undefined behavior when attempting to do so.
        /// </summary>
        /// <param name="processId">The ID of the process to attach to.</param>
        /// <param name="suspend">Whether or not to suspend the process.</param>
        /// <param name="symbolCredential">The TokenCredential to use for any Azure based symbol servers (set to null if not using one).</param>
        /// <returns>A <see cref="DataTarget"/> instance.</returns>
        public static DataTarget AttachToProcess(int processId, bool suspend, TokenCredential? symbolCredential = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsProcessDataReaderMode mode = suspend ? WindowsProcessDataReaderMode.Suspend : WindowsProcessDataReaderMode.Passive;
                return new DataTarget(new CustomDataTarget(new WindowsProcessDataReader(processId, mode), symbolCredential));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new DataTarget(new CustomDataTarget(new LinuxLiveDataReader(processId, suspend), symbolCredential));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new DataTarget(new CustomDataTarget(new MacOSProcessDataReader(processId, suspend), symbolCredential));
            }

            throw GetPlatformException();
        }

        /// <summary>
        /// Creates a snapshot of a running process and attaches to it.  This method will pause a running process
        ///
        /// </summary>
        /// <param name="processId">The ID of the process to attach to.</param>
        /// <param name="symbolCredential">The TokenCredential to use for any Azure based symbol servers (set to null if not using one).</param>
        /// <returns>A <see cref="DataTarget"/> instance.</returns>
        /// <exception cref="ArgumentException">
        /// The process specified by <paramref name="processId"/> is not running.
        /// </exception>
        /// <exception cref="PlatformNotSupportedException">
        /// The current platform is not Windows.
        /// </exception>
        public static DataTarget CreateSnapshotAndAttach(int processId, TokenCredential? symbolCredential = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CustomDataTarget customTarget = new(new WindowsProcessDataReader(processId, WindowsProcessDataReaderMode.Snapshot), symbolCredential);
                return new DataTarget(customTarget);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new DataTarget(LinuxSnapshotTarget.CreateSnapshotFromProcess(processId));
            }

            throw GetPlatformException();
        }

        /// <summary>
        /// Creates a DataTarget from an IDebugClient interface.  This allows callers to interop with the DbgEng debugger
        /// (cdb.exe, windbg.exe, dbgeng.dll).
        /// </summary>
        /// <param name="pDebugClient">An IDebugClient interface.</param>
        /// <param name="symbolCredential">A TokenCredential for azure based symbol servers.</param>
        /// <returns>A <see cref="DataTarget"/> instance.</returns>
        public static DataTarget CreateFromDbgEng(IntPtr pDebugClient, TokenCredential? symbolCredential = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw GetPlatformException();

            CustomDataTarget customTarget = new(new DbgEngDataReader(pDebugClient), symbolCredential);
            return new DataTarget(customTarget);
        }

        private static PlatformNotSupportedException GetPlatformException([CallerMemberName] string? method = null) =>
            new($"{method} is not supported on {RuntimeInformation.OSDescription}.");
    }
}