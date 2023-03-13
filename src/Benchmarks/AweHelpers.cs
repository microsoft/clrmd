// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;

namespace Benchmarks
{
    internal static class AweHelpers
    {
        [SupportedOSPlatform("windows")]
        public static bool IsAweEnabled => EnableDisablePrivilege("SeLockMemoryPrivilege", enable: true);

        [SupportedOSPlatform("windows")]
        private static bool EnableDisablePrivilege(string PrivilegeName, bool enable)
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TokenAccessLevels.AdjustPrivileges | TokenAccessLevels.Query, out IntPtr processToken))
                return false;

            TOKEN_PRIVILEGES tokenPrivleges = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Privileges = new LUID_AND_ATTRIBUTES[1] };

            if (!LookupPrivilegeValue(lpSystemName: null, PrivilegeName, out LUID luid))
                return false;

            tokenPrivleges.Privileges[0].LUID = luid;
            tokenPrivleges.Privileges[0].Attributes = enable ? LuidAttributes.Enabled : LuidAttributes.Disabled;
            if (AdjustTokenPrivileges(processToken, disableAllPrivleges: false, ref tokenPrivleges, bufferLength: (uint)Marshal.SizeOf(typeof(TOKEN_PRIVILEGES)), out _, out _) == 0)
                return false;

            int returnCode = Marshal.GetLastWin32Error();
            return returnCode != ERROR_NOT_ALL_ASSIGNED;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public LUID_AND_ATTRIBUTES[] Privileges;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID LUID;

            [MarshalAs(UnmanagedType.U4)]
            public LuidAttributes Attributes;
        }

        private enum LuidAttributes : uint
        {
            Disabled = 0x00000000,
            EnabledByDefault = 0x00000001,
            Enabled = 0x00000002,
            PrivelegedUsedForAccess = 0x80000000
        }


        [DllImport("advapi32", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, TokenAccessLevels desiredAccess, out IntPtr processToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivleges, ref TOKEN_PRIVILEGES newState, uint bufferLength, out TOKEN_PRIVILEGES previousState, out uint returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        private const int ERROR_NOT_ALL_ASSIGNED = 1300;
    }
}
