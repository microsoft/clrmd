// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Infers clr info from module names, provides corresponding dac details
    /// </summary>
    public static class ClrInfoProvider
    {
        private const string c_desktopModuleName1 = "clr";
        private const string c_desktopModuleName2 = "mscorwks";
        private const string c_coreModuleName = "coreclr";
        private const string c_linuxCoreModuleName = "libcoreclr";

        private const string c_desktopDacFileNameBase = "mscordacwks";
        private const string c_coreDacFileNameBase = "mscordaccore";
        private const string c_desktopDacFileName = c_desktopDacFileNameBase + ".dll";
        private const string c_coreDacFileName = c_coreDacFileNameBase + ".dll";
        private const string c_linuxCoreDacFileName = "libmscordaccore.so";

        private static bool TryGetModuleName(ModuleInfo moduleInfo, out string moduleName)
        {
            moduleName = Path.GetFileNameWithoutExtension(moduleInfo.FileName);
            if (moduleName == null)
                return false;

            moduleName = moduleName.ToLower();
            return true;
        }

        /// <summary>
        /// Checks if the provided module corresponds to a supported runtime, gets clr details inferred from the module name.
        /// </summary>
        /// <param name="moduleInfo">Module info</param>
        /// <param name="flavor">Clr flavor</param>
        /// <param name="platform">Platform</param>
        /// <returns>true if module corresponds to a supported runtime</returns>
        public static bool IsSupportedRuntime(ModuleInfo moduleInfo, out ClrFlavor flavor, out Platform platform)
        {
            flavor = default;
            platform = default;

            if (!TryGetModuleName(moduleInfo, out var moduleName))
                return false;

            switch (moduleName)
            {
                case c_desktopModuleName1:
                case c_desktopModuleName2:
                    flavor = ClrFlavor.Desktop;
                    platform = Platform.Windows;
                    return true;

                case c_coreModuleName:
                    flavor = ClrFlavor.Core;
                    platform = Platform.Windows;
                    return true;

                case c_linuxCoreModuleName:
                    flavor = ClrFlavor.Core;
                    platform = Platform.Linux;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the filename of the dac dll according to the specified parameters
        /// </summary>
        public static string GetDacFileName(ClrFlavor flavor, Platform platform)
        {
            if (platform == Platform.Linux)
                return c_linuxCoreDacFileName;

            return flavor == ClrFlavor.Core ? c_coreDacFileName : c_desktopDacFileName;
        }

        /// <summary>
        /// Returns the filename of the dac dll for the requests to the symbol server
        /// </summary>
        public static string GetDacRequestFileName(ClrFlavor flavor, Architecture currentArchitecture, Architecture targetArchitecture, VersionInfo version, Platform platform)
        {
            // Linux never has a "long" named DAC
            if (platform == Platform.Linux)
                return c_linuxCoreDacFileName;

            var dacNameBase = flavor == ClrFlavor.Core ? c_coreDacFileNameBase : c_desktopDacFileNameBase;
            return $"{dacNameBase}_{currentArchitecture}_{targetArchitecture}_{version.Major}.{version.Minor}.{version.Revision}.{version.Patch:D2}.dll";
        }
    }
}