// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents the dac dll.
    /// </summary>
    public class DacInfo : ModuleInfo
    {
        /// <summary>
        /// Gets the platform-agnostic file name of the dac dll.
        /// </summary>
        public string PlatformAgnosticFileName { get; }

        /// <summary>
        /// Gets the architecture (x86 or amd64) being targeted.
        /// </summary>
        public Architecture TargetArchitecture { get; }

        /// <summary>
        /// Constructs a DacInfo object with the appropriate properties initialized.
        /// </summary>
        public DacInfo(
            IDataReader reader,
            string agnosticName,
            Architecture targetArch,
            ulong imgBase,
            uint filesize,
            uint timestamp,
            string fileName,
            VersionInfo version,
            ImmutableArray<byte> buildId = default)
            : base(reader, imgBase, filesize, timestamp, fileName, buildId, version)
        {
            PlatformAgnosticFileName = agnosticName;
            TargetArchitecture = targetArch;
        }
    }
}