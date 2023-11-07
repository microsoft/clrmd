// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.Interfaces;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents information about a single CLR in a process.
    /// </summary>
    public sealed class ClrInfo : IClrInfo
    {
        internal ClrInfo(DataTarget dt, ModuleInfo module, Version clrVersion, IClrInfoProvider provider)
        {
            DataTarget = dt ?? throw new ArgumentNullException(nameof(dt));
            ModuleInfo = module ?? throw new ArgumentNullException(nameof(module));
            ClrInfoProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            Version = clrVersion ?? throw new ArgumentNullException(nameof(clrVersion));
        }

        /// <summary>
        /// The DataTarget containing this ClrInfo.
        /// </summary>
        public DataTarget DataTarget { get; }

        /// <summary>
        /// The IClrInfoProvider which created this ClrInfo.
        /// </summary>
        internal IClrInfoProvider ClrInfoProvider { get; }

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
        /// Creates a runtime from the given DAC file on disk.  This is equivalent to
        /// CreateRuntime(dacPath, ignoreMismatch: false).
        /// </summary>
        /// <param name="dacPath">A full path to the matching DAC dll for this process.</param>
        /// <returns>The runtime associated with this CLR.</returns>
        public ClrRuntime CreateRuntime(string dacPath)
        {
            if (string.IsNullOrEmpty(dacPath))
                throw new ArgumentNullException(nameof(dacPath));

            if (!File.Exists(dacPath))
                throw new FileNotFoundException(dacPath);

            return CreateRuntimeWorker(dacPath, ignoreMismatch: false);
        }

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

        /// <summary>
        /// Creates a runtime by searching for the correct dac to load.
        /// </summary>
        /// <returns>The runtime associated with this CLR.</returns>
        public ClrRuntime CreateRuntime() => CreateRuntimeWorker(null, ignoreMismatch: false);

        private ClrRuntime CreateRuntimeWorker(string? dacPath, bool ignoreMismatch)
        {
            IServiceProvider services = ClrInfoProvider.GetDacServices(this, dacPath, ignoreMismatch);
            return new ClrRuntime(this, services);
        }

        IClrRuntime IClrInfo.CreateRuntime() => CreateRuntime();

        IClrRuntime IClrInfo.CreateRuntime(string dacPath) => CreateRuntime(dacPath);

        IClrRuntime IClrInfo.CreateRuntime(string dacPath, bool ignoreMismatch) => CreateRuntime(dacPath, ignoreMismatch);
    }
}