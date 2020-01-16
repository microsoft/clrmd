// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal unsafe sealed class WindowsFunctions : PlatformFunctions
    {
        internal static bool IsProcessRunning(int processId)
        {
            IntPtr handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_INFORMATION, false, processId);
            if (handle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(handle);
                return true;
            }

            int minimumLength = 256;
            int[] processIds = ArrayPool<int>.Shared.Rent(minimumLength);
            try
            {
                int size;
                for (; ; )
                {
                    NativeMethods.EnumProcesses(processIds, processIds.Length * sizeof(int), out size);
                    if (size == processIds.Length * sizeof(int))
                    {
                        ArrayPool<int>.Shared.Return(processIds);
                        minimumLength *= 2;
                        processIds = ArrayPool<int>.Shared.Rent(minimumLength);
                        continue;
                    }

                    break;
                }

                return Array.IndexOf(processIds, processId, 0, size / sizeof(int)) >= 0;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(processIds);
            }
        }

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

            byte[] buffer = ArrayPool<byte>.Shared.Rent(len);
            try
            {
                fixed (byte* data = buffer)
                {
                    if (!NativeMethods.GetFileVersionInfo(dll, handle, len, data))
                        return false;

                    if (!NativeMethods.VerQueryValue(data, "\\", out IntPtr ptr, out len))
                        return false;

                    DebugOnly.Assert(unchecked((int)ptr.ToInt64()) % sizeof(ushort) == 0);

                    minor = Unsafe.Read<ushort>((ptr + 8).ToPointer());
                    major = Unsafe.Read<ushort>((ptr + 10).ToPointer());
                    patch = Unsafe.Read<ushort>((ptr + 12).ToPointer());
                    revision = Unsafe.Read<ushort>((ptr + 14).ToPointer());

                    return true;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
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
            private const string VersionLibraryName = "version.dll";

            public const int PROCESS_QUERY_INFORMATION = 0x0400;

            [DllImport(Kernel32LibraryName, SetLastError = true)]
            public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

            [DllImport(Kernel32LibraryName, SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport(Kernel32LibraryName, SetLastError = true)]
            public static extern unsafe bool EnumProcesses(int[] lpidProcess, int cb, out int lpcbNeeded);

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

            [DllImport(Kernel32LibraryName)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool isWow64);

            [DllImport(VersionLibraryName)]
            public static extern bool GetFileVersionInfo(string sFileName, int handle, int size, byte* infoBuffer);

            [DllImport(VersionLibraryName)]
            public static extern int GetFileVersionInfoSize(string sFileName, out int handle);

            [DllImport(VersionLibraryName)]
            public static extern bool VerQueryValue(byte* pBlock, string pSubBlock, out IntPtr val, out int len);

            public static short IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR = 14;

            [DllImport(Kernel32LibraryName)]
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