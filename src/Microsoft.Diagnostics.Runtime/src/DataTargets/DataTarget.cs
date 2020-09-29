// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Linux;
using Microsoft.Diagnostics.Runtime.MacOS;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A crash dump or live process to read out of.
    /// </summary>
    public sealed class DataTarget : IDisposable
    {
        private readonly CustomDataTarget _target;
        private bool _disposed;
        private ImmutableArray<ClrInfo> _clrs;
        private ModuleInfo[]? _modules;
        private string? _symlink;
        private readonly Dictionary<string, PEImage?> _pefileCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();

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
        public IBinaryLocator? BinaryLocator { get => _target.BinaryLocator; set => _target.BinaryLocator = value; }

        /// <summary>
        /// Creates a DataTarget from the given reader.
        /// </summary>
        /// <param name="customTarget">The custom data target to use.</param>
        public DataTarget(CustomDataTarget customTarget)
        {
            _target = customTarget ?? throw new ArgumentNullException(nameof(customTarget));
            DataReader = _target.DataReader;
            CacheOptions = _target.CacheOptions ?? new CacheOptions();

            IBinaryLocator? locator = _target.BinaryLocator;
            if (locator == null)
            {
                if (DataReader.TargetPlatform == OSPlatform.Windows)
                {
                    string sympath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH") ?? "http://msdl.microsoft.com/download/symbols";
                    locator = new SymbolServerLocator(sympath);
                }
                else if (DataReader.TargetPlatform == OSPlatform.Linux)
                {
                    locator = new LinuxDefaultSymbolLocator(DataReader);
                }
                else
                {
                    locator = new MacOSDefaultSymbolLocator();
                }
            }

            BinaryLocator = locator;
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

                if (_symlink != null)
                {
                    try
                    {   
                        File.Delete(_symlink);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                    }
                }

                _target.Dispose();
                _disposed = true;
            }
        }

        internal PEImage? LoadPEImage(string path)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DataTarget));

            if (string.IsNullOrEmpty(path))
                return null;

            PEImage? result;

            lock (_pefileCache)
            {
                if (_pefileCache.TryGetValue(path, out result))
                    return result;
            }

            result = new PEImage(File.OpenRead(path));

            if (!result.IsValid)
                result = null;

            lock (_pefileCache)
            {
                // We may have raced with another thread and that thread put a value here first
                if (_pefileCache.TryGetValue(path, out PEImage? cached) && cached != null)
                {
                    result?.Dispose(); // We don't need this instance now.
                    return cached;
                }

                return _pefileCache[path] = result;
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

        private ImmutableArray<ClrInfo> GetOrCreateClrVersions()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DataTarget));

            if (!_clrs.IsDefault)
                return _clrs;

            Architecture arch = DataReader.Architecture;
            ImmutableArray<ClrInfo>.Builder versions = ImmutableArray.CreateBuilder<ClrInfo>(2);
            foreach (ModuleInfo module in EnumerateModules())
            {
                if (!ClrInfoProvider.IsSupportedRuntime(module, out var flavor, out OSPlatform platform))
                    continue;

                string dacFileName = ClrInfoProvider.GetDacFileName(flavor, platform);
                string? dacLocation = Path.Combine(Path.GetDirectoryName(module.FileName)!, dacFileName);

                if (platform == OSPlatform.Linux)
                {
                    if (File.Exists(dacLocation))
                    {
                        // Works around issue https://github.com/dotnet/coreclr/issues/20205
                        lock (_sync)
                        {
                            _symlink = Path.GetTempFileName();
                            if (LinuxFunctions.symlink(dacLocation, _symlink) == 0)
                            {
                                dacLocation = _symlink;
                            }
                        }
                    }
                    else
                    {
                        dacLocation = null;
                    }
                }
                else if (!File.Exists(dacLocation) || !PlatformFunctions.IsEqualFileVersion(dacLocation, module.Version))
                {
                    dacLocation = null;
                }

                VersionInfo version = module.Version;
                string dacAgnosticName = ClrInfoProvider.GetDacRequestFileName(flavor, arch, arch, version, platform);
                string dacRegularName = ClrInfoProvider.GetDacRequestFileName(flavor, IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64, arch, version, platform);

                DacInfo dacInfo = new DacInfo(dacLocation, dacRegularName, dacAgnosticName, arch, module.IndexFileSize, module.IndexTimeStamp, module.Version, module.BuildId);
                versions.Add(new ClrInfo(this, flavor, module, dacInfo));
            }

            _clrs = versions.MoveOrCopyToImmutable();
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

#pragma warning disable CS0618 // Type or member is obsolete
            foreach (ModuleInfo module in modules)
                module.DataTarget = this;
#pragma warning restore CS0618 // Type or member is obsolete

            return _modules = modules;
        }

        /// <summary>
        /// Gets a set of helper functions that are consistently implemented across all platforms.
        /// </summary>
        public static PlatformFunctions PlatformFunctions { get; } =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? new LinuxFunctions() :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? new MacOSFunctions() :
            (PlatformFunctions)new WindowsFunctions();

        /// <summary>
        /// Loads a dump stream. Currently supported formats are ELF coredump and Windows Minidump formats.
        /// </summary>
        /// <param name="displayName">The name of this DataTarget, might be used in exceptions.</param>
        /// <param name="stream">The stream that should be used.</param>
        /// <param name="cacheOptions">The caching options to use. (Only used for FileStreams)</param>
        /// <param name="leaveOpen">True whenever the given stream should be leaved open when the DataTarget is disposed.</param>
        /// <returns>A <see cref="DataTarget"/> for the given dump.</returns>
        public static DataTarget LoadDump(string displayName, Stream stream, CacheOptions? cacheOptions = null, bool leaveOpen = false)
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

#pragma warning disable CA2000 // Dispose objects before losing scope

                IDataReader reader = format switch
                {
                    DumpFileFormat.Minidump => new MinidumpReader(displayName, stream, cacheOptions, leaveOpen),
                    DumpFileFormat.ElfCoredump => new CoredumpReader(displayName, stream, leaveOpen),

                    // USERDU64 dumps are the "old" style of dumpfile.  This file format is very old and shouldn't be
                    // used.  However, IDebugClient::WriteDumpFile(,DEBUG_DUMP_DEFAULT) still generates this format
                    // (at least with the Win10 system32\dbgeng.dll), so we will support this for now.
                    DumpFileFormat.Userdump64 => new DbgEngDataReader(displayName, stream, leaveOpen),
                    DumpFileFormat.CompressedArchive => throw new InvalidDataException($"Stream '{displayName}' is a compressed archived instead of a dump file."),
                    _ => throw new InvalidDataException($"Stream '{displayName}' is in an unknown or unsupported file format."),
                };

                return new DataTarget(new CustomDataTarget(reader) {CacheOptions = cacheOptions});

#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            catch
            {
                if (leaveOpen)
                    stream?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Loads a dump file. Currently supported formats are ELF coredump and Windows Minidump formats.
        /// </summary>
        /// <param name="filePath">The path to the dump file.</param>
        /// <param name="cacheOptions">The caching options to use.</param>
        /// <returns>A <see cref="DataTarget"/> for the given dump file.</returns>
        public static DataTarget LoadDump(string filePath, CacheOptions? cacheOptions = null)
        {
            if (filePath is null)
                throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Could not open dump file '{filePath}'.", filePath);

#pragma warning disable CA2000 // Dispose objects before losing scope - LoadDump(Stream) will take ownership
            FileStream stream = File.OpenRead(filePath);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return LoadDump(filePath, stream, cacheOptions, leaveOpen: false);
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
        /// <returns>A <see cref="DataTarget"/> instance.</returns>
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
        public static DataTarget AttachToProcess(int processId, bool suspend)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsProcessDataReaderMode mode = suspend ? WindowsProcessDataReaderMode.Suspend : WindowsProcessDataReaderMode.Passive;
                return new DataTarget(new CustomDataTarget(new WindowsProcessDataReader(processId, mode)));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new DataTarget(new CustomDataTarget(new LinuxLiveDataReader(processId, suspend)));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new DataTarget(new CustomDataTarget(new MacOSProcessDataReader(processId, suspend)));
            }

            throw GetPlatformException();
        }

        /// <summary>
        /// Creates a snapshot of a running process and attaches to it.  This method will pause a running process
        /// 
        /// </summary>
        /// <param name="processId">The ID of the process to attach to.</param>
        /// <returns>A <see cref="DataTarget"/> instance.</returns>
        /// <exception cref="ArgumentException">
        /// The process specified by <paramref name="processId"/> is not running.
        /// </exception>
        /// <exception cref="PlatformNotSupportedException">
        /// The current platform is not Windows.
        /// </exception>
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
        public static DataTarget CreateSnapshotAndAttach(int processId)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CustomDataTarget customTarget = new CustomDataTarget(new WindowsProcessDataReader(processId, WindowsProcessDataReaderMode.Snapshot));
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
        /// <returns>A <see cref="DataTarget"/> instance.</returns>
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
        public static DataTarget CreateFromDbgEng(IntPtr pDebugClient)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw GetPlatformException();

            CustomDataTarget customTarget = new CustomDataTarget(new DbgEngDataReader(pDebugClient));
            return new DataTarget(customTarget);
        }

        private static Exception GetPlatformException([CallerMemberName] string? method = null) =>
            new PlatformNotSupportedException($"{method} is not supported on {RuntimeInformation.OSDescription}.");
    }
}
