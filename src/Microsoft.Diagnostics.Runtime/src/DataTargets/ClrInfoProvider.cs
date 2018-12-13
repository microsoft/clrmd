// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class ClrInfoProvider
    {
        private const string c_desktopModuleName1 = "clr";
        private const string c_desktopModuleName2 = "mscorwks";
        private const string c_coreModuleName = "coreclr";
        private const string c_linuxCoreModuleName = "libcoreclr";

        private static bool TryGetModuleName(ModuleInfo moduleInfo, out string moduleName)
        {
            moduleName = Path.GetFileNameWithoutExtension(moduleInfo.FileName);
            if (moduleName == null)
                return false;

            moduleName = moduleName.ToLower();
            return true;
        }

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

        private static string GetDacModuleName(ClrFlavor flavor)
        {
            return flavor == ClrFlavor.Core ? "mscordaccore" : "mscordacwks";
        }

        private static string GetExtension(Platform platform)
        {
            if (platform == Platform.Windows)
                return ".dll";

            if (platform == Platform.Linux)
                return ".so";

            throw new ArgumentException("Unknown platform: " + platform);
        }

        public static string GetDacFileName(ClrFlavor flavor, Platform platform)
        {
            return GetDacModuleName(flavor) + GetExtension(platform);
        }

        public static string GetDacRequestFileName(ClrFlavor flavor, Architecture currentArchitecture, Architecture targetArchitecture, VersionInfo version, Platform platform)
        {
            var dacName = GetDacModuleName(flavor);
            var extension = GetExtension(platform);
            return $"{dacName}_{currentArchitecture}_{targetArchitecture}_{version.Major}.{version.Minor}.{version.Revision}.{version.Patch:D2}{extension}";
        }
    }
}