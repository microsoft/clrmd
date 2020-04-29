// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// This class provides information needed to located the correct CLR diagnostics DLL (this dll
    /// is called the Debug Access Component (DAC)).
    /// </summary>
    public sealed class DacInfo
    {
        /// <summary>
        /// If a local dac exists on disk that matches this dac, this property will contain its full path.
        /// In a live debugging scenario this will almost always point to a local dac which can be used to inspect
        /// the process (unless the user deleted mscordaccore in the .Net Core case).
        /// </summary>
        public string? LocalDacPath { get; }

        /// <summary>
        /// Gets the platform specific filename of the DAC dll.
        /// </summary>
        public string PlatformSpecificFileName { get; }

        /// <summary>
        /// Gets the platform-agnostic file name of the DAC dll.
        /// </summary>
        public string PlatformAgnosticFileName { get; }

        /// <summary>
        /// Gets the architecture (x86 or amd64) being targeted.
        /// </summary>
        public Architecture TargetArchitecture { get; }

        /// <summary>
        /// Gets the specific file size of the image used to index it on the symbol server.
        /// </summary>
        public int IndexFileSize { get; }

        /// <summary>
        /// Gets the timestamp of the image used to index it on the symbol server.
        /// </summary>
        public int IndexTimeStamp { get; }

        /// <summary>
        /// Gets the version information for the CLR this dac matches.  The dac will have the
        /// same version.
        /// </summary>
        public VersionInfo Version { get; }

        /// <summary>
        /// If CLR has a build id on this platform, this property will contain its build id.
        /// </summary>
        public ImmutableArray<byte> ClrBuildId { get; }

        /// <summary>
        /// Constructs a DacInfo object with the appropriate properties initialized.
        /// </summary>
        public DacInfo(string? localPath, string specificName, string agnosticName, Architecture targetArch,
                       int filesize, int timestamp, VersionInfo version, ImmutableArray<byte> clrBuildId)
        {
            LocalDacPath = localPath;
            PlatformSpecificFileName = specificName;
            PlatformAgnosticFileName = agnosticName;
            TargetArchitecture = targetArch;
            IndexFileSize = filesize;
            IndexTimeStamp = timestamp;
            Version = version;
            ClrBuildId = clrBuildId;
        }
    }
}
