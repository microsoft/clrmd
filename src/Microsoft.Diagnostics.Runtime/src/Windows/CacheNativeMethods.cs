// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal static class CacheNativeMethods
    {
        internal static class File
        {
            internal static IntPtr CreateFile(string fileName, FileMode mode)
            {
                return CreateFile(fileName, mode, (mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite));
            }

            internal static IntPtr CreateFile(string fileName, FileMode mode, FileAccess access)
            {
                return CreateFile(fileName, mode, access, FileShare.Read);
            }

            internal static IntPtr CreateFile(string fileName, FileMode mode, FileAccess access, FileShare share)
            {
                return CreateFile(fileName, access, share, securityAttributes: IntPtr.Zero, mode, FileAttributes.Normal, templateFile: IntPtr.Zero);
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr CreateFile([MarshalAs(UnmanagedType.LPTStr)] string filename,
                                                    [MarshalAs(UnmanagedType.U4)] FileAccess access,
                                                    [MarshalAs(UnmanagedType.U4)] FileShare share,
                                                    IntPtr securityAttributes,
                                                    [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
                                                    [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
                                                    IntPtr templateFile);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr handle);

            internal static bool ReadFile(IntPtr hFile, IntPtr buffer, uint numberOfBytesToRead, out uint numberOfBytesRead)
            {
                return ReadFile(hFile, buffer, numberOfBytesToRead, out numberOfBytesRead, lpOverlapped: IntPtr.Zero);
            }

            internal static bool ReadFile(IntPtr hFile, UIntPtr buffer, uint numberOfBytesToRead, out uint numberOfBytesRead)
            {
                return ReadFile(hFile, buffer, numberOfBytesToRead, out numberOfBytesRead, lpOverlapped: IntPtr.Zero);
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool ReadFile(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool ReadFile(IntPtr hFile, UIntPtr lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

            internal static bool SetFilePointerEx(IntPtr file, long distanceToMove, SeekOrigin seekOrigin)
            {
                return SetFilePointerEx(file, distanceToMove, lpNewFilePointer: IntPtr.Zero, seekOrigin);
            }

            [DllImport("kernel32.dll")]
            private static extern bool SetFilePointerEx(IntPtr hFile, long liDistanceToMove, IntPtr lpNewFilePointer, [MarshalAs(UnmanagedType.U4)] SeekOrigin dwMoveMethod);
        }

        internal static class Memory
        {
            [Flags]
            internal enum MemoryProtection : uint
            {
                NoAccess = 0x00000001,
                ReadOnly = 0x00000002,
                ReadWrite = 0x00000004,
                WriteCopy = 0x00000008,
                Execute = 0x00000010,
                ExecuteRead = 0x00000020,
                ExecuteReadWrite = 0x00000040,
                ExecuteWriteCopy = 0x00000080,
                Guard = 0x00000100,
                NoCache = 0x00000200,
                WriteCombine = 0x00000200,
                TargetsInvalid = 0x40000000
            }

            [Flags]
            internal enum VirtualAllocType : uint
            {
                Commit = 0x00001000,
                Reserve = 0x00002000,
                Reset = 0x00080000,
                TopDown = 0x00100000,
                WriteWatch = 0x00200000,
                Physical = 0x00400000,
                ResetUndo = 0x01000000,
                LargePages = 0x20000000,
            }

            internal enum VirtualFreeType : uint
            {
                CoalescePlaceholders = 0x00000001,
                PreservePlaceholder = 0x00000002,
                Decommit = 0x00004000,
                Release = 0x00008000
            }

            internal static UIntPtr VirtualAlloc(uint allocSize, VirtualAllocType allocType, MemoryProtection memoryProtection)
            {
                return VirtualAlloc(lpAddress: UIntPtr.Zero, new UIntPtr(allocSize), allocType, memoryProtection);
            }

            [DllImport("kernel32", SetLastError = true)]
            private static extern UIntPtr VirtualAlloc(UIntPtr lpAddress, UIntPtr allocSize, [MarshalAs(UnmanagedType.U4)] VirtualAllocType allocationType, [MarshalAs(UnmanagedType.U4)] MemoryProtection protection);

            [DllImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool VirtualFree(UIntPtr lpAddress, UIntPtr sizeToFree, [MarshalAs(UnmanagedType.U4)] VirtualFreeType freeType);

            [DllImport("kernel32", SetLastError = true)]
            internal static extern IntPtr GetProcessHeap();

            internal enum HeapFlags : uint
            {
                None               = 0x00000000,
                NoSerialize        = 0x00000001,
                GenerateExceptions = 0x00000004,
                ZeroMemory         = 0x00000008
            }

            internal static UIntPtr HeapAlloc(uint bytesRequested)
            {
                return HeapAlloc(GetProcessHeap(), HeapFlags.None, new UIntPtr(bytesRequested));
            }

            [DllImport("kernel32")]
            private static extern UIntPtr HeapAlloc(IntPtr heapHandle, [MarshalAs(UnmanagedType.U4)] HeapFlags heapFlags, UIntPtr bytesRequested);

            internal static bool HeapFree(UIntPtr memory)
            {
                return HeapFree(GetProcessHeap(), HeapFlags.None, memory);
            }

            [DllImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool HeapFree(IntPtr heapHandle, [MarshalAs(UnmanagedType.U4)] HeapFlags heapFlags, UIntPtr lpMem);


            internal static uint HeapSize(UIntPtr heapAddress)
            {
                UIntPtr heapSize = HeapSize(GetProcessHeap(), 0, heapAddress);
                return heapSize.ToUInt32();
            }

            [DllImport("kernel32", SetLastError = true)]
            private static extern UIntPtr HeapSize(IntPtr heap, uint flags, UIntPtr lpMem);

            [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
            internal static extern UIntPtr memcpy(UIntPtr dest, UIntPtr src, UIntPtr count);

            [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
            internal static extern UIntPtr memcpy(IntPtr dest, UIntPtr src, UIntPtr count);
        }

        internal static class AWE
        {
            internal static bool AllocateUserPhysicalPages(ref uint numberOfPages, UIntPtr pageArray)
            {
                UIntPtr numberOfPagesRequested = new UIntPtr(numberOfPages);
                bool res = AllocateUserPhysicalPages(Process.GetCurrentProcess().Handle, ref numberOfPagesRequested, pageArray);
                numberOfPages = numberOfPagesRequested.ToUInt32();

                return res;
            }

            [DllImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool AllocateUserPhysicalPages(IntPtr processHandle, ref UIntPtr numberOfPages, UIntPtr pageArray);


            internal static bool MapUserPhysicalPages(UIntPtr virtualAddress, uint numberOfPages, UIntPtr pageArray)
            {
                UIntPtr numberOfPagesToMap = new UIntPtr(numberOfPages);
                return MapUserPhysicalPages(virtualAddress, numberOfPagesToMap, pageArray);
            }

            [DllImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool MapUserPhysicalPages(UIntPtr virtualAddress, UIntPtr numberOfPages, UIntPtr pageArray);

            internal static bool FreeUserPhysicalPages(ref uint numberfOfPages, UIntPtr pageArray)
            {
                UIntPtr numberOfPagesToFree = new UIntPtr(numberfOfPages);
                bool res = FreeUserPhysicalPages(Process.GetCurrentProcess().Handle, ref numberOfPagesToFree, pageArray);
                numberfOfPages = numberOfPagesToFree.ToUInt32();

                return res;
            }

            [DllImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool FreeUserPhysicalPages(IntPtr processHandle, ref UIntPtr numberOfPages, UIntPtr pageArray);
        }

        internal static class Util
        {
            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

            [StructLayout(LayoutKind.Sequential)]
            internal struct SYSTEM_INFO
            {
                internal ushort wProcessorArchitecture;
                internal ushort wReserved;
                internal uint dwPageSize;
                internal IntPtr lpMinimumApplicationAddress;
                internal IntPtr lpMaximumApplicationAddress;
                internal IntPtr dwActiveProcessorMask;
                internal uint dwNumberOfProcessors;
                internal uint dwProcessorType;
                internal uint dwAllocationGranularity;
                internal short wProcessorLevel;
                internal short wProcessorRevision;
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

            internal static bool EnableDisablePrivilege(string PrivilegeName, bool enable)
            {
                IntPtr processToken;
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TokenAccessLevels.AdjustPrivileges | TokenAccessLevels.Query, out processToken))
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                TOKEN_PRIVILEGES tokenPrivleges = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Privileges = new LUID_AND_ATTRIBUTES[1] };

                LUID luid;
                if (!LookupPrivilegeValue(lpSystemName: null, PrivilegeName, out luid))
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                tokenPrivleges.Privileges[0].LUID = luid;
                tokenPrivleges.Privileges[0].Attributes = enable ? LuidAttributes.Enabled : LuidAttributes.Disabled;

                TOKEN_PRIVILEGES previousPrivileges;
                uint previousPrivilegeLength;
                if (AdjustTokenPrivileges(processToken, disableAllPrivleges: false, ref tokenPrivleges, bufferLength: (uint)Marshal.SizeOf(typeof(TOKEN_PRIVILEGES)), out previousPrivileges, out previousPrivilegeLength) == 0)
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                int returnCode = Marshal.GetLastWin32Error();
                if (returnCode == ERROR_NOT_ALL_ASSIGNED)
                {
                    return false;
                }

                return true;
            }

            private const int ERROR_NOT_ALL_ASSIGNED = 1300;


            private enum LuidAttributes : uint
            {
                Disabled = 0x00000000,
                EnabledByDefault = 0x00000001,
                Enabled = 0x00000002,
                PrivelegedUsedForAccess = 0x80000000
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

            [StructLayout(LayoutKind.Sequential)]
            private struct TOKEN_PRIVILEGES
            {
                public uint PrivilegeCount;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
                public LUID_AND_ATTRIBUTES[] Privileges;
            }

            [DllImport("advapi32", SetLastError = true)]
            private static extern bool OpenProcessToken(IntPtr processHandle, TokenAccessLevels desiredAccess, out IntPtr processToken);

            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern int AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivleges, ref TOKEN_PRIVILEGES newState, uint bufferLength, out TOKEN_PRIVILEGES previousState, out uint returnLength);

            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        }
    }

    //
    // Summary:
    //     Defines the privileges of the user account associated with the access token.
    [Flags]
    public enum TokenAccessLevels
    {
        //
        // Summary:
        //     The user can attach a primary token to a process.
        AssignPrimary = 1,
        //
        // Summary:
        //     The user can duplicate the token.
        Duplicate = 2,
        //
        // Summary:
        //     The user can impersonate a client.
        Impersonate = 4,
        //
        // Summary:
        //     The user can query the token.
        Query = 8,
        //
        // Summary:
        //     The user can query the source of the token.
        QuerySource = 16,
        //
        // Summary:
        //     The user can enable or disable privileges in the token.
        AdjustPrivileges = 32,
        //
        // Summary:
        //     The user can change the attributes of the groups in the token.
        AdjustGroups = 64,
        //
        // Summary:
        //     The user can change the default owner, primary group, or discretionary access
        //     control list (DACL) of the token.
        AdjustDefault = 128,
        //
        // Summary:
        //     The user can adjust the session identifier of the token.
        AdjustSessionId = 256,
        //
        // Summary:
        //     The user has standard read rights and the System.Security.Principal.TokenAccessLevels.Query
        //     privilege for the token.
        Read = 131080,
        //
        // Summary:
        //     The user has standard write rights and the System.Security.Principal.TokenAccessLevels.AdjustPrivileges,
        //     System.Security.Principal.TokenAccessLevels.AdjustGroups and System.Security.Principal.TokenAccessLevels.AdjustDefault
        //     privileges for the token.
        Write = 131296,
        //
        // Summary:
        //     The user has all possible access to the token.
        AllAccess = 983551,
        //
        // Summary:
        //     The maximum value that can be assigned for the System.Security.Principal.TokenAccessLevels
        //     enumeration.
        MaximumAllowed = 33554432
    }
}
