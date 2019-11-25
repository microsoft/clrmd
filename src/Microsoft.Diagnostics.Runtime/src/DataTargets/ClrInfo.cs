// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Buffers;
using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents information about a single Clr runtime in a process.
    /// </summary>
    public sealed class ClrInfo
    {
        private readonly DataTarget _dataTarget;
        
        internal ClrRuntime Runtime { get; }
        
        internal ClrInfo(DataTarget dt, ClrFlavor flavor, ModuleInfo module, DacInfo dacInfo, string dacLocation)
        {
            _dataTarget = dt ?? throw new ArgumentNullException(nameof(dt));
            Flavor = flavor;
            DacInfo = dacInfo ?? throw new ArgumentNullException(nameof(dacInfo));
            ModuleInfo = module ?? throw new ArgumentNullException(nameof(module));
            LocalMatchingDac = dacLocation;
        }

        internal void Dispose()
        {
            // Intentionally internal and not IDisposable.
            Runtime?.Dispose();
        }

        /// <summary>
        /// The version number of this runtime.
        /// </summary>
        public VersionInfo Version => ModuleInfo.Version;

        /// <summary>
        /// The type of CLR this module represents.
        /// </summary>
        public ClrFlavor Flavor { get; }

        /// <summary>
        /// Returns module information about the Dac needed create a ClrRuntime instance for this runtime.
        /// </summary>
        public DacInfo DacInfo { get; }

        /// <summary>
        /// Returns module information about the ClrInstance.
        /// </summary>
        public ModuleInfo ModuleInfo { get; }

        /// <summary>
        /// Returns the location of the local dac on your machine which matches this version of Clr, or null
        /// if one could not be found.
        /// </summary>
        public string LocalMatchingDac { get; }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A version string for this Clr runtime.</returns>
        public override string ToString() => Version.ToString();

        /// <summary>
        /// Creates a runtime from the given Dac file on disk.
        /// </summary>
        /// <param name="dacFilename">A full path to the matching mscordacwks for this process.</param>
        /// <param name="ignoreMismatch">Whether or not to ignore mismatches between </param>
        /// <returns></returns>
        public ClrRuntime CreateRuntime(string dacFilename, bool ignoreMismatch = false)
        {
            ThrowIfRuntimeCreated();
            if (string.IsNullOrEmpty(dacFilename)) throw new ArgumentNullException(nameof(dacFilename));

            if (!File.Exists(dacFilename))
                throw new FileNotFoundException(dacFilename);

            if (!ignoreMismatch)
            {
                DataTarget.PlatformFunctions.GetFileVersion(dacFilename, out int major, out int minor, out int revision, out int patch);
                if (major != Version.Major || minor != Version.Minor || revision != Version.Revision || patch != Version.Patch)
                    throw new InvalidOperationException($"Mismatched dac. Version: {major}.{minor}.{revision}.{patch}");
            }

            return ConstructRuntime(dacFilename);
        }

        public ClrRuntime CreateRuntime()
        {
            ThrowIfRuntimeCreated();
            string dac = LocalMatchingDac;
            if (dac != null && !File.Exists(dac))
                dac = null;

            if (dac == null)
                dac = _dataTarget.SymbolLocator.FindBinary(DacInfo);

            if (!File.Exists(dac))
                throw new FileNotFoundException("Could not find matching DAC for this runtime.", DacInfo.FileName);

            if (IntPtr.Size != _dataTarget.DataReader.PointerSize)
                throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

            return ConstructRuntime(dac);
        }

        private void ThrowIfRuntimeCreated()
        {
            if (Runtime != null)
                throw new InvalidOperationException($"ClrRuntime for version {Version} has already been created.");
        }


        private ClrRuntime ConstructRuntime(string dac)
        {
            if (IntPtr.Size != (int)_dataTarget.DataReader.PointerSize)
                throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

            DacLibrary lib = new DacLibrary(_dataTarget, dac);

            if (Flavor == ClrFlavor.Core)
                return new V45Runtime(this, _dataTarget, lib);

            DesktopVersion ver;
            if (Version.Major == 2)
                ver = DesktopVersion.v2;
            else if (Version.Major == 4 && Version.Minor == 0 && Version.Patch < 10000)
                ver = DesktopVersion.v4;
            else
            {
                // Assume future versions will all work on the newest runtime version.
                return new V45Runtime(this, _dataTarget, lib);
            }

            return new LegacyRuntime(this, _dataTarget, lib, ver, Version.Patch);
        }
    }
}