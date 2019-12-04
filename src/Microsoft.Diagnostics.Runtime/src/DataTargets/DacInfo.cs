// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

#pragma warning disable 0618

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents the dac dll.
    /// </summary>
    public class DacInfo : ModuleInfo
    {
        /// <summary>
        /// The platform-agnostic filename of the dac dll.
        /// </summary>
        public string PlatformAgnosticFileName { get; }

        /// <summary>
        /// The architecture (x86 or amd64) being targeted.
        /// </summary>
        public Architecture TargetArchitecture { get; }

        /// <summary>
        /// Constructs a DacInfo object with the appropriate properties initialized.
        /// </summary>
        public DacInfo(IDataReader reader, string agnosticName, Architecture targetArch, ulong imgBase,
                        uint filesize, uint timestamp, string filename, VersionInfo version, IReadOnlyList<byte> buildId = null)
            : base(reader, imgBase, filesize, timestamp, filename, buildId, version)
        {
            PlatformAgnosticFileName = agnosticName;
            TargetArchitecture = targetArch;
        }
    }
}