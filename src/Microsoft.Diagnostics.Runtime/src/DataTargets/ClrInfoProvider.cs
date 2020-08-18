// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Infers clr info from module names, provides corresponding DAC details.
    /// </summary>
    public static class ClrInfoProvider
    {
        private const string c_desktopModuleName1 = "clr.dll";
        private const string c_desktopModuleName2 = "mscorwks.dll";
        private const string c_coreModuleName = "coreclr.dll";
        private const string c_linuxCoreModuleName = "libcoreclr.so";
        private const string c_macOSCoreModuleName = "libcoreclr.dylib";

        private const string c_desktopDacFileNameBase = "mscordacwks";
        private const string c_coreDacFileNameBase = "mscordaccore";
        private const string c_desktopDacFileName = c_desktopDacFileNameBase + ".dll";
        private const string c_coreDacFileName = c_coreDacFileNameBase + ".dll";
        private const string c_linuxCoreDacFileName = "libmscordaccore.so";
        private const string c_macOSCoreDacFileName = "libmscordaccore.dylib";

        /// <summary>
        /// Checks if the provided module corresponds to a supported runtime, gets clr details inferred from the module name.
        /// </summary>
        /// <param name="moduleInfo">Module info.</param>
        /// <param name="flavor">CLR flavor.</param>
        /// <param name="platform">Platform.</param>
        /// <returns>true if module corresponds to a supported runtime.</returns>
        public static bool IsSupportedRuntime(ModuleInfo moduleInfo, out ClrFlavor flavor, out OSPlatform platform)
        {
            if (moduleInfo is null)
                throw new ArgumentNullException(nameof(moduleInfo));

            flavor = default;
            platform = default;

            string? moduleName = Path.GetFileName(moduleInfo.FileName);
            if (moduleName is null)
                return false;

            switch (moduleName)
            {
                case c_desktopModuleName1:
                case c_desktopModuleName2:
                    flavor = ClrFlavor.Desktop;
                    platform = OSPlatform.Windows;
                    return true;

                case c_coreModuleName:
                    flavor = ClrFlavor.Core;
                    platform = OSPlatform.Windows;
                    return true;

                case c_linuxCoreModuleName:
                    flavor = ClrFlavor.Core;
                    platform = OSPlatform.Linux;
                    return true;

                case c_macOSCoreModuleName:
                    flavor = ClrFlavor.Core;
                    platform = OSPlatform.OSX;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the file name of the DAC dll according to the specified parameters.
        /// </summary>
        public static string GetDacFileName(ClrFlavor flavor, OSPlatform platform)
        {
            if (platform == OSPlatform.Linux)
                return c_linuxCoreDacFileName;

            if (platform == OSPlatform.OSX)
                return c_macOSCoreDacFileName;

            return flavor == ClrFlavor.Core ? c_coreDacFileName : c_desktopDacFileName;
        }

        /// <summary>
        /// Returns the file name of the DAC dll for the requests to the symbol server.
        /// </summary>
        public static string GetDacRequestFileName(ClrFlavor flavor, Architecture currentArchitecture, Architecture targetArchitecture, VersionInfo version, OSPlatform platform)
        {
            // Linux never has a "long" named DAC
            if (platform == OSPlatform.Linux)
                return c_linuxCoreDacFileName;

            if (platform == OSPlatform.OSX)
                return c_macOSCoreDacFileName;

            var dacNameBase = flavor == ClrFlavor.Core ? c_coreDacFileNameBase : c_desktopDacFileNameBase;
            return $"{dacNameBase}_{currentArchitecture}_{targetArchitecture}_{version.Major}.{version.Minor}.{version.Revision}.{version.Patch:D2}.dll";
        }
    }
}