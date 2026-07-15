// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacImplementation;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Interfaces;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents information about a single CLR in a process.
    /// </summary>
    public sealed class ClrInfo : IClrInfo
    {
        /// <summary>
        /// Constructs a <see cref="ClrInfo"/> for a runtime whose DAC data-access interface
        /// (<c>IXCLRDataProcess</c>) is supplied by the host through
        /// <see cref="DataTarget.AddLoadedRuntime(ClrInfo, IntPtr, object?)"/>. Use this when the host
        /// (e.g. SOS) performs its own runtime detection and DAC/cDAC construction instead of relying on
        /// ClrMD's discovery.
        /// </summary>
        public ClrInfo(DataTarget dt, ModuleInfo module, Version clrVersion)
        {
            DataTarget = dt ?? throw new ArgumentNullException(nameof(dt));
            ModuleInfo = module ?? throw new ArgumentNullException(nameof(module));
            Version = clrVersion ?? throw new ArgumentNullException(nameof(clrVersion));
        }

        /// <summary>
        /// The DataTarget containing this ClrInfo.
        /// </summary>
        public DataTarget DataTarget { get; }

        /// <summary>
        /// A factory that produces the host-owned <c>IXCLRDataProcess</c> pointer for this runtime, set by
        /// <see cref="DataTarget.AddLoadedRuntime(ClrInfo, System.Func{IntPtr}, object?)"/>. When non-null,
        /// <see cref="CreateRuntime()"/> uses it instead of resolving and loading a DAC for this runtime.
        /// </summary>
        internal Func<IntPtr>? ClrDataProcessFactory { get; set; }

        /// <summary>
        /// The lock that serializes DAC calls for a host-supplied runtime. May be shared with the host so its
        /// own DAC usage is serialized against ClrMD's.
        /// </summary>
        internal object? DacLock { get; set; }

        IDataTarget IClrInfo.DataTarget => DataTarget;

        /// <summary>
        /// Gets the version number of this runtime.
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Returns whether this CLR was built as a single file executable.
        /// </summary>
        public bool IsSingleFile { get; set; }

        /// <summary>
        /// Gets the type of CLR this module represents.
        /// </summary>
        public ClrFlavor Flavor { get; set; }

        /// <summary>
        /// A list of debugging libraries associated associated with this .Net runtime.
        /// This can contain both the dac (used by ClrMD) and the DBI (not used by ClrMD).
        /// </summary>
        public ImmutableArray<DebugLibraryInfo> DebuggingLibraries { get; set; }

        /// <summary>
        /// Gets module information about the ClrInstance.
        /// </summary>
        public ModuleInfo ModuleInfo { get; }

        /// <summary>
        /// The CDAC contract export address
        /// </summary>
        public ulong ContractDescriptorAddress { get; set; }

        /// <summary>
        /// The timestamp under which this CLR is is archived (0 if this module is indexed under
        /// a BuildId instead).  Note that this may be a different value from ModuleInfo.IndexTimeStamp.
        /// In a single-file scenario, the ModuleInfo will be the info of the program's main executable
        /// and not CLR's properties.
        /// </summary>
        public int IndexTimeStamp { get; set; }

        /// <summary>
        /// The filesize under which this CLR is is archived (0 if this module is indexed under
        /// a BuildId instead).  Note that this may be a different value from ModuleInfo.IndexFileSize.
        /// In a single-file scenario, the ModuleInfo will be the info of the program's main executable
        /// and not CLR's properties.
        /// </summary>
        public int IndexFileSize { get; set; }

        /// <summary>
        /// The BuildId under which this CLR is archived.  BuildId.IsEmptyOrDefault will be true if
        /// this runtime is archived under file/timesize instead.
        /// </summary>
        public ImmutableArray<byte> BuildId { get; set; } = ImmutableArray<byte>.Empty;

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A version string for this CLR.</returns>
        public override string ToString() => Version.ToString();

        /// <summary>
        /// Creates a runtime by searching for the correct dac to load.
        /// </summary>
        /// <returns>The runtime associated with this CLR.</returns>
        public ClrRuntime CreateRuntime() => CreateRuntimeWorker(null, ignoreMismatch: false);

        /// <summary>
        /// Creates a runtime from the given DAC file on disk.  This is equivalent to
        /// CreateRuntime(dacPath, ignoreMismatch: false).
        /// </summary>
        /// <param name="dacPath">A full path to the matching DAC dll for this process.</param>
        /// <returns>The runtime associated with this CLR.</returns>
        public ClrRuntime CreateRuntime(string dacPath) => CreateRuntime(dacPath, ignoreMismatch: false);

        /// <summary>
        /// Creates a runtime from the given DAC file on disk.
        /// </summary>
        /// <param name="dacPath">A full path to the matching DAC dll for this process.</param>
        /// <param name="ignoreMismatch">Whether or not to ignore mismatches between. </param>
        /// <returns>The runtime associated with this CLR.</returns>
        public ClrRuntime CreateRuntime(string dacPath, bool ignoreMismatch)
        {
            if (string.IsNullOrEmpty(dacPath))
                throw new ArgumentNullException(nameof(dacPath));

            if (!File.Exists(dacPath))
                throw new FileNotFoundException(dacPath);

            return CreateRuntimeWorker(dacPath, ignoreMismatch);
        }


        private ClrRuntime CreateRuntimeWorker(string? dacPath, bool ignoreMismatch)
        {
            try
            {
                IServiceProvider services;
                if (ClrDataProcessFactory is not null)
                {
                    // Host-supplied runtime: build DAC services over the host's IXCLRDataProcess instead of
                    // loading a DAC ourselves. The provided dacPath/ignoreMismatch do not apply here.
                    IntPtr clrDataProcess = ClrDataProcessFactory();
                    if (clrDataProcess == IntPtr.Zero)
                        throw new ClrDiagnosticsException("The IXCLRDataProcess factory returned a null pointer.");

                    services = new DacServiceProvider(this, clrDataProcess, DacLock ?? new object());
                }
                else
                {
                    // Normal path: resolve and load a matching DAC for this runtime, then build DAC services.
                    DacLibrary library = GetDacLibraryFromPath(dacPath, ignoreMismatch, DataTarget.Options.VerifyDacOnWindows);
                    services = new DacServiceProvider(this, library);
                }

                return new ClrRuntime(this, services);
            }
            catch (Exception ex) when (DataTarget.DataReader is IDumpInfoProvider { IsCreatedByDotNetRuntime: false } provider)
            {
                throw new ClrDiagnosticsException(
                    $"Failed to create ClrRuntime and the dump was not collected by the .NET runtime's " +
                    $"createdump tool. System/kernel dumps may be missing memory required for .NET diagnostics. " +
                    $"Recollect the dump using createdump or set DOTNET_DbgEnableMiniDump=1. Original error: {ex.Message}",
                    ex);
            }
        }

        private DacLibrary GetDacLibraryFromPath(string? dacPath, bool ignoreMismatch, bool verifySignature)
        {
            if (dacPath is not null)
                return CreateDacFromPath(dacPath, ignoreMismatch, verifySignature);

            OSPlatform currentPlatform = DotNetClrInfoProvider.GetCurrentPlatform();
            Architecture currentArch = RuntimeInformation.ProcessArchitecture;
            Exception? exception = null;
            bool foundOne = false;

            IFileLocator? locator = DataTarget.FileLocator;

            foreach (DebugLibraryInfo dac in DebuggingLibraries.Where(r => r.Kind == DebugLibraryKind.Dac && r.Platform == currentPlatform && r.TargetArchitecture == currentArch))
            {
                foundOne = true;

                // If we have a full path, use it.  We already validated that the CLR matches.
                if (Path.GetFileName(dac.FileName) != dac.FileName)
                {
                    dacPath = dac.FileName;
                }
                else
                {
                    // The properties we are requesting under may not be the actual file properties, so don't request them.

                    if (locator != null)
                    {
                        if (!dac.IndexBuildId.IsDefaultOrEmpty)
                        {
                            dacPath = locator.FindPEImage(dac.FileName, SymbolProperties.Coreclr, dac.IndexBuildId, DataTarget.DataReader.TargetPlatform, checkProperties: false);
                        }
                        else if (dac.IndexTimeStamp != 0 && dac.IndexFileSize != 0)
                        {
                            if (dac.Platform == OSPlatform.Windows)
                                dacPath = DataTarget.FileLocator?.FindPEImage(dac.FileName, dac.IndexTimeStamp, dac.IndexFileSize, checkProperties: false);
                        }
                    }
                }

                if (dacPath is not null && File.Exists(dacPath))
                {
                    try
                    {
                        // If we get the file from the symbol server, assume mismatches are expected.  Sometimes we replace dacs on the symbol
                        // server to fix bugs.  If it's archived under the right path, use it.
                        return CreateDacFromPath(dacPath, ignoreMismatch: true, verifySignature);
                    }
                    catch (Exception ex)
                    {
                        exception ??= ex;
                        dacPath = null;
                    }
                }
            }

            if (exception is not null)
                throw exception;

            // We should have had at least one dac enumerated if this is a supported scenario.
            if (!foundOne)
                throw new InvalidOperationException($"Debugging a '{DataTarget.DataReader.TargetPlatform}' crash is not supported on '{currentPlatform}'.");

            if (currentPlatform == OSPlatform.Windows)
                throw new FileNotFoundException("Could not find matching DAC for this runtime.");

            throw new FileNotFoundException("Could not find matching DAC for this runtime.  Note that symbol server download of the DAC is disabled for this platform.");
        }

        private DacLibrary CreateDacFromPath(string dacPath, bool ignoreMismatch, bool verifySignature)
        {
            if (!File.Exists(dacPath))
                throw new FileNotFoundException(dacPath);

            if (!ignoreMismatch && !IsSingleFile)
            {
                DataTarget.PlatformFunctions.GetFileVersion(dacPath, out int major, out int minor, out int revision, out int patch);
                if (major != Version.Major || minor != Version.Minor || revision != Version.Build || patch != Version.Revision)
                    throw new ClrDiagnosticsException($"Mismatched dac. Dac version: {major}.{minor}.{revision}.{patch}, expected: {Version}.");
            }

            return new(DataTarget, dacPath, ModuleInfo.ImageBase, ContractDescriptorAddress, verifySignature);
        }

        IClrRuntime IClrInfo.CreateRuntime() => CreateRuntime();

        IClrRuntime IClrInfo.CreateRuntime(string dacPath) => CreateRuntime(dacPath);

        IClrRuntime IClrInfo.CreateRuntime(string dacPath, bool ignoreMismatch) => CreateRuntime(dacPath, ignoreMismatch);
    }
}