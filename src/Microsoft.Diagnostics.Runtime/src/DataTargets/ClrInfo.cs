// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
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
        public Version Version => ModuleInfo.Version;

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
        /// If the application is single-file, this contains the runtime, DAC and DBI index information
        /// </summary>
        public ClrRuntimeInfo? SingleFileRuntimeInfo { get; internal set; }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A version string for this CLR.</returns>
        public override string ToString() => Version.ToString();

        /// <summary>
        /// Creates a runtime from the given DAC file on disk.
        /// </summary>
        /// <param name="dacPath">A full path to the matching DAC dll for this process.</param>
        /// <param name="ignoreMismatch">Whether or not to ignore mismatches between. </param>
        /// <returns></returns>
        public ClrRuntime CreateRuntime(string dacPath, bool ignoreMismatch = false)
        {
            if (string.IsNullOrEmpty(dacPath))
                throw new ArgumentNullException(nameof(dacPath));

            if (!File.Exists(dacPath))
                throw new FileNotFoundException(dacPath);

            if (!ignoreMismatch)
            {
                DataTarget.PlatformFunctions.GetFileVersion(dacPath, out int major, out int minor, out int revision, out int patch);
                if (major != Version.Major || minor != Version.Minor || revision != Version.Build || patch != Version.Revision)
                    throw new InvalidOperationException($"Mismatched dac. Dac version: {major}.{minor}.{revision}.{patch}, expected: {Version}.");
            }

            return ConstructRuntime(dacPath);
        }

        public ClrRuntime CreateRuntime()
        {
            string? dac = DacInfo.LocalDacPath;
            if (dac != null && !File.Exists(dac))
                dac = null;

            if (IntPtr.Size != DataTarget.DataReader.PointerSize)
                throw new InvalidOperationException("Mismatched pointer size between this process and the dac.");

            if (DataTarget.DataReader.TargetPlatform == OSPlatform.Windows)
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    ThrowCrossDebugError();

                dac ??= DataTarget.FileLocator?.FindPEImage(DacInfo.PlatformSpecificFileName, DacInfo.IndexTimeStamp, DacInfo.IndexFileSize, checkProperties: false);
            }
            else if (DataTarget.DataReader.TargetPlatform == OSPlatform.Linux)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    dac ??= DataTarget.FileLocator?.FindPEImage(DacInfo.PlatformSpecificFileName, SymbolProperties.Coreclr, DacInfo.ClrBuildId, DataTarget.DataReader.TargetPlatform, checkProperties: false);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    dac ??= DataTarget.FileLocator?.FindElfImage(DacInfo.PlatformSpecificFileName, SymbolProperties.Coreclr, DacInfo.ClrBuildId, checkProperties: false);
                else
                    ThrowCrossDebugError();
            }
            else if (DataTarget.DataReader.TargetPlatform == OSPlatform.OSX)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    dac ??= DataTarget.FileLocator?.FindMachOImage(DacInfo.PlatformSpecificFileName, SymbolProperties.Coreclr, DacInfo.ClrBuildId, checkProperties: false);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    dac ??= DataTarget.FileLocator?.FindPEImage(DacInfo.PlatformSpecificFileName, SymbolProperties.Coreclr, DacInfo.ClrBuildId, DataTarget.DataReader.TargetPlatform, checkProperties: false);
                else
                    ThrowCrossDebugError();
            }
            else
            {
                ThrowCrossDebugError();
            }


            if (!File.Exists(dac))
                throw new FileNotFoundException("Could not find matching DAC for this runtime.", DacInfo.PlatformSpecificFileName);

            return ConstructRuntime(dac!);
        }

        private void ThrowCrossDebugError()
        {
            throw new InvalidOperationException($"Debugging a {DataTarget.DataReader.TargetPlatform} crash is not supported on this operating system.");
        }

#pragma warning disable CA2000 // Dispose objects before losing scope
        private ClrRuntime ConstructRuntime(string dac)
        {
            if (IntPtr.Size != DataTarget.DataReader.PointerSize)
                throw new InvalidOperationException("Mismatched pointer size between this process and the dac.");

            DacLibrary dacLibrary = new DacLibrary(DataTarget, dac, ModuleInfo.ImageBase);
            DacInterface.SOSDac? sos = dacLibrary.SOSDacInterface;
            if (sos is null)
                throw new InvalidOperationException($"Could not create a ISOSDac pointer from this dac library: {dac}");

            var factory = new RuntimeBuilder(this, dacLibrary, sos);
            if (Flavor == ClrFlavor.Core)
                return factory.GetOrCreateRuntime();

            if (Version.Major < 4 || (Version.Major == 4 && Version.Minor == 5 && Version.Revision < 10000))
                throw new NotSupportedException($"CLR version '{Version}' is not supported by ClrMD.  For Desktop CLR, only CLR 4.6 and beyond are supported.");

            return factory.GetOrCreateRuntime();
        }
    }
}
