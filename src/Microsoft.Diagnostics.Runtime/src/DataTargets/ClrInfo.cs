// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents information about a single Clr runtime in a process.
    /// </summary>
    [Serializable]
    public class ClrInfo
    {
        internal ClrInfo(DataTarget dataTarget, ModuleInfo module, ClrFlavor flavor, Architecture architecture, bool isLinux)
        {
            ModuleInfo = module ?? throw new ArgumentNullException(nameof(module));
            DataTarget = dataTarget ?? throw new ArgumentNullException(nameof(dataTarget));
            Flavor = flavor;
            Architecture = architecture;
            IsLinux = isLinux;
        }

        /// <summary>
        /// The version number of this runtime.
        /// </summary>
        public VersionInfo Version => ModuleInfo.Version;

        /// <summary>
        /// The type of CLR this module represents.
        /// </summary>
        public ClrFlavor Flavor { get; }

        public Architecture Architecture { get; }

        public bool IsLinux { get; }

        /// <summary>
        /// Returns module information about the ClrInstance.
        /// </summary>
        public ModuleInfo ModuleInfo { get; }

        public string DacFileName => ClrInfoProvider.GetDacFileName(Flavor, IsLinux);
        public string DacRequestFileName => ClrInfoProvider.GetDacRequestFileName(Flavor, Architecture, Architecture, Version, IsLinux);

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A version string for this Clr runtime.</returns>
        public override string ToString()
        {
            return Version.ToString();
        }

        //for backward compatibility only
        internal DataTarget DataTarget { get; }
    }
}