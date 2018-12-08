// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class ClrInfoProvider
    {
        private const string c_desktopModuleName1 = "clr";
        private const string c_desktopModuleName2 = "mscorwks";
        private const string c_coreModuleName = "coreclr";
        private const string c_linuxCoreModuleName = "libcoreclr";
        private const string c_nativeModuleName = "mrt100_app";

        private static bool TryGetModuleName(ModuleInfo moduleInfo, out string moduleName)
        {
            moduleName = Path.GetFileNameWithoutExtension(moduleInfo.FileName);
            if (moduleName == null)
                return false;

            moduleName = moduleName.ToLower();
            return true;
        }
    
        public static bool IsSupportedRuntime(ModuleInfo moduleInfo, out ClrFlavor flavor, out bool isLinux)
        {
            flavor = default;
            isLinux = false;

            if (!TryGetModuleName(moduleInfo, out var moduleName))
                return false;

            switch (moduleName)
            {
                case c_desktopModuleName1:
                case c_desktopModuleName2:
                    flavor = ClrFlavor.Desktop;
                    return true;

                case c_coreModuleName:
                    flavor = ClrFlavor.Core;
                    return true;
                
                case c_linuxCoreModuleName:
                    flavor = ClrFlavor.Core;
                    isLinux = true;
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsNativeRuntime(ModuleInfo moduleInfo)
        {
            if (!TryGetModuleName(moduleInfo, out var moduleName))
                return false;

            return moduleName == c_nativeModuleName;
        }

        private static string GetDacModuleName(ClrFlavor flavor)
        {
            return flavor == ClrFlavor.Core ? "mscordaccore" : "mscordacwks";
        }
        
        private static string GetExtension(bool isLinux)
        {
            return isLinux ? ".so" : ".dll";
        }
    
        public static string GetDacFileName(ClrFlavor flavor, bool isLinux)
        {
            return GetDacModuleName(flavor) + GetExtension(isLinux);
        }
    
        public static string GetDacRequestFileName(ClrFlavor flavor, Architecture currentArchitecture, Architecture targetArchitecture, VersionInfo clrVersion, bool isLinux)
        {
            var dacName = GetDacModuleName(flavor);
            var extension = GetExtension(isLinux);
            return $"{dacName}_{currentArchitecture}_{targetArchitecture}_{clrVersion.Major}.{clrVersion.Minor}.{clrVersion.Revision}.{clrVersion.Patch:D2}{extension}";
        }
    }
}