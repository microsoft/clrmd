// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal sealed class WindowsFunctions : PlatformFunctions
    {
        public override bool FreeLibrary(IntPtr module)
        {
            return NativeMethods.FreeLibrary(module);
        }

        internal override bool GetFileVersion(string dll, out int major, out int minor, out int revision, out int patch)
        {
            major = minor = revision = patch = 0;

            int len = NativeMethods.GetFileVersionInfoSize(dll, out int handle);
            if (len <= 0)
                return false;

            byte[] data = new byte[len];
            if (!NativeMethods.GetFileVersionInfo(dll, handle, len, data))
                return false;

            if (!NativeMethods.VerQueryValue(data, "\\", out IntPtr ptr, out len))
                return false;

            byte[] vsFixedInfo = new byte[len];
            Marshal.Copy(ptr, vsFixedInfo, 0, len);

            minor = (ushort)Marshal.ReadInt16(vsFixedInfo, 8);
            major = (ushort)Marshal.ReadInt16(vsFixedInfo, 10);
            patch = (ushort)Marshal.ReadInt16(vsFixedInfo, 12);
            revision = (ushort)Marshal.ReadInt16(vsFixedInfo, 14);

            return true;
        }

        public override IntPtr GetProcAddress(IntPtr module, string method)
        {
            return NativeMethods.GetProcAddress(module, method);
        }

        public override IntPtr LoadLibrary(string lpFileName)
        {
            return NativeMethods.LoadLibraryEx(lpFileName, 0, NativeMethods.LoadLibraryFlags.NoFlags);
        }

        internal static class NativeMethods
        {
            private const string Kernel32LibraryName = "kernel32.dll";

            public const uint FILE_MAP_READ = 4;

            [DllImport(Kernel32LibraryName)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeLibrary(IntPtr hModule);

            public static IntPtr LoadLibrary(string lpFileName)
            {
                return LoadLibraryEx(lpFileName, 0, LoadLibraryFlags.NoFlags);
            }

            [DllImport(Kernel32LibraryName, SetLastError = true)]
            public static extern IntPtr LoadLibraryEx(string fileName, int hFile, LoadLibraryFlags dwFlags);

            [Flags]
            public enum LoadLibraryFlags : uint
            {
                NoFlags = 0x00000000,
                DontResolveDllReferences = 0x00000001,
                LoadIgnoreCodeAuthzLevel = 0x00000010,
                LoadLibraryAsDatafile = 0x00000002,
                LoadLibraryAsDatafileExclusive = 0x00000040,
                LoadLibraryAsImageResource = 0x00000020,
                LoadWithAlteredSearchPath = 0x00000008
            }

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool isWow64);

            [DllImport("version.dll")]
            public static extern bool GetFileVersionInfo(string sFileName, int handle, int size, byte[] infoBuffer);

            [DllImport("version.dll")]
            public static extern int GetFileVersionInfoSize(string sFileName, out int handle);

            [DllImport("version.dll")]
            public static extern bool VerQueryValue(byte[] pBlock, string pSubBlock, out IntPtr val, out int len);

            private const int VS_FIXEDFILEINFO_size = 0x34;
            public static short IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR = 14;

            [DllImport("kernel32.dll")]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);
        }

        public override bool TryGetWow64(IntPtr proc, out bool result)
        {
            if (Environment.OSVersion.Version.Major > 5 ||
                Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1)
            {
                return NativeMethods.IsWow64Process(proc, out result);
            }

            result = false;
            return false;
        }
    }
}