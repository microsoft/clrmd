// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#pragma warning disable 0618

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents the dac dll
    /// </summary>
    [Serializable]
    public class DacInfo : ModuleInfo
    {
        /// <summary>
        /// Returns the filename of the dac dll according to the specified parameters
        /// </summary>
        public static string GetDacRequestFileName(ClrFlavor flavor, Architecture currentArchitecture, Architecture targetArchitecture, VersionInfo clrVersion)
        {
            string dacName = flavor == ClrFlavor.Core ? "mscordaccore" : "mscordacwks";
            return string.Format(
                "{0}_{1}_{2}_{3}.{4}.{5}.{6:D2}.dll",
                dacName,
                currentArchitecture,
                targetArchitecture,
                clrVersion.Major,
                clrVersion.Minor,
                clrVersion.Revision,
                clrVersion.Patch);
        }

        internal static string GetDacFileName(ClrFlavor flavor, Architecture targetArchitecture)
        {
            return flavor == ClrFlavor.Core ? "mscordaccore.dll" : "mscordacwks.dll";
        }

        /// <summary>
        /// The platform-agnostic filename of the dac dll
        /// </summary>
        public string PlatformAgnosticFileName { get; set; }

        /// <summary>
        /// The architecture (x86 or amd64) being targeted
        /// </summary>
        public Architecture TargetArchitecture { get; set; }

        /// <summary>
        /// Constructs a DacInfo object with the appropriate properties initialized
        /// </summary>
        public DacInfo(IDataReader reader, string agnosticName, Architecture targetArch)
            : base(reader)
        {
            PlatformAgnosticFileName = agnosticName;
            TargetArchitecture = targetArch;
        }
    }
}