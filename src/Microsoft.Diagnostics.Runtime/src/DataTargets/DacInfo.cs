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
            var isWin = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var prefix = isWin ? "" : "lib";
            var ext = isWin ? "dll" : "so";
            string dacName = flavor == ClrFlavor.Core ? "mscordaccore" : "mscordacwks";
            // ReSharper disable once UseStringInterpolation
            return string.Format(
                "{0}{1}_{2}_{3}_{4}.{5}.{6}.{7:D2}.{8}",
                prefix,
                dacName,
                currentArchitecture,
                targetArchitecture,
                clrVersion.Major,
                clrVersion.Minor,
                clrVersion.Revision,
                clrVersion.Patch,
                ext);
        }

        internal static string GetDacFileName(ClrFlavor flavor, Architecture targetArchitecture)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return flavor == ClrFlavor.Core ? "mscordaccore.dll" : "mscordacwks.dll";
            }
            else
            {
                return flavor == ClrFlavor.Core ? "libmscordaccore.so" : "libmscordacwks.so";
            }
        }

        /// <summary>
        /// The platform-agnostice filename of the dac dll
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