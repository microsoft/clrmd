// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Diagnostics.Runtime.Builders;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents information about a single CLR in a process.
    /// </summary>
    public sealed class ClrInfo
    {
        public DataTarget DataTarget { get; }

        internal ClrInfo(DataTarget dt, ClrFlavor flavor, ModuleInfo module, DacInfo dacInfo)
        {
            DataTarget = dt ?? throw new ArgumentNullException(nameof(dt));
            Flavor = flavor;
            DacInfo = dacInfo ?? throw new ArgumentNullException(nameof(dacInfo));
            ModuleInfo = module ?? throw new ArgumentNullException(nameof(module));
        }

        /// <summary>
        /// Gets the version number of this runtime.
        /// </summary>
        public VersionInfo Version => ModuleInfo.Version;

        /// <summary>
        /// Gets the type of CLR this module represents.
        /// </summary>
        public ClrFlavor Flavor { get; }

        /// <summary>
        /// Gets module information about the DAC needed create a <see cref="ClrRuntime"/> instance for this runtime.
        /// </summary>
        public DacInfo DacInfo { get; }

        /// <summary>
        /// Gets module information about the ClrInstance.
        /// </summary>
        public ModuleInfo ModuleInfo { get; }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A version string for this CLR.</returns>
        public override string ToString() => Version.ToString();

        /// <summary>
        /// Creates a runtime from the given DAC file on disk.
        /// </summary>
        /// <param name="dacFilename">A full path to the matching DAC dll for this process.</param>
        /// <param name="ignoreMismatch">Whether or not to ignore mismatches between. </param>
        /// <returns></returns>
        public ClrRuntime CreateRuntime(string dacFilename, bool ignoreMismatch = false)
        {
            if (string.IsNullOrEmpty(dacFilename))
                throw new ArgumentNullException(nameof(dacFilename));

            if (!File.Exists(dacFilename))
                throw new FileNotFoundException(dacFilename);

            if (!ignoreMismatch)
            {
                DataTarget.PlatformFunctions.GetFileVersion(dacFilename, out int major, out int minor, out int revision, out int patch);
                if (major != Version.Major || minor != Version.Minor || revision != Version.Revision || patch != Version.Patch)
                    throw new InvalidOperationException($"Mismatched dac. Dac version: {major}.{minor}.{revision}.{patch}, expected: {Version}.");
            }

            return ConstructRuntime(dacFilename);
        }

        public ClrRuntime CreateRuntime()
        {
            string? dac = DacInfo.LocalDacPath;
            if (dac != null && !File.Exists(dac))
                dac = null;

            if (DacInfo.PlatformSpecificFileName != null)
                dac ??= DataTarget.BinaryLocator?.FindBinary(DacInfo.PlatformSpecificFileName, DacInfo.IndexTimeStamp, DacInfo.IndexFileSize, checkProperties: false);

            if (!File.Exists(dac))
                throw new FileNotFoundException("Could not find matching DAC for this runtime.", DacInfo.PlatformSpecificFileName);

            if (IntPtr.Size != DataTarget.DataReader.PointerSize)
                throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

            return ConstructRuntime(dac!);
        }

#pragma warning disable CA2000 // Dispose objects before losing scope
        private ClrRuntime ConstructRuntime(string dac)
        {
            if (IntPtr.Size != DataTarget.DataReader.PointerSize)
                throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

            DacLibrary dacLibrary = new DacLibrary(DataTarget, dac);
            DacInterface.SOSDac? sos = dacLibrary.SOSDacInterface;
            if (sos is null)
                throw new InvalidOperationException($"Could not create a ISOSDac pointer from this dac library: {dac}");

            var factory = new RuntimeBuilder(this, dacLibrary, sos);
            if (Flavor == ClrFlavor.Core)
                return factory.GetOrCreateRuntime();

            if (Version.Major < 4 || (Version.Major == 4 && Version.Minor == 5 && Version.Patch < 10000))
                throw new NotSupportedException($"CLR version '{Version}' is not supported by ClrMD.  For Desktop CLR, only CLR 4.6 and beyond are supported.");

            return factory.GetOrCreateRuntime();
        }
    }
}
