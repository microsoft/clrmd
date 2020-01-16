// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    internal unsafe class LiveDataReader : IDataReader
    {
        private bool _disposed = false;
        private readonly int _originalPid;
        private readonly IntPtr _snapshotHandle;
        private readonly IntPtr _cloneHandle;
        private readonly IntPtr _process;
        private readonly int _pid;

        private const int PROCESS_VM_READ = 0x10;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;

        public LiveDataReader(int processId, bool createSnapshot)
        {
            if (createSnapshot)
            {
                _originalPid = processId;

                // Throws
                Process process = Process.GetProcessById(processId);
                int hr = PssCaptureSnapshot(process.Handle, PSS_CAPTURE_FLAGS.PSS_CAPTURE_VA_CLONE, IntPtr.Size == 8 ? 0x0010001F : 0x0001003F, out _snapshotHandle);
                if (hr != 0)
                    throw new ClrDiagnosticsException($"Could not create snapshot to process. Error {hr}.", ClrDiagnosticsExceptionKind.Unknown, hr);

                hr = PssQuerySnapshot(_snapshotHandle, PSS_QUERY_INFORMATION_CLASS.PSS_QUERY_VA_CLONE_INFORMATION, out _cloneHandle, IntPtr.Size);
                if (hr != 0)
                    throw new ClrDiagnosticsException($"Could not query the snapshot. Error {hr}.", ClrDiagnosticsExceptionKind.Unknown, hr);

                _pid = GetProcessId(_cloneHandle);
            }
            else
            {
                _pid = processId;
            }

            _process = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _pid);

            if (_process == IntPtr.Zero)
            {
                if (!WindowsFunctions.IsProcessRunning(_pid))
                    throw new ArgumentException("The process is not running");

                int hr = Marshal.GetLastWin32Error();
                throw new ClrDiagnosticsException($"Could not attach to process. Error {hr}.", ClrDiagnosticsExceptionKind.Unknown, hr);
            }

            using Process p = Process.GetCurrentProcess();
            if (DataTarget.PlatformFunctions.TryGetWow64(p.Handle, out bool wow64)
                && DataTarget.PlatformFunctions.TryGetWow64(_process, out bool targetWow64)
                && wow64 != targetWow64)
            {
                throw new ClrDiagnosticsException("Dac architecture mismatch!", ClrDiagnosticsExceptionKind.DacError);
            }
        }

        private void Dispose(bool _)
        {
            if (!_disposed)
            {
                if (_originalPid != 0)
                {
                    int hr = PssFreeSnapshot(Process.GetCurrentProcess().Handle, _snapshotHandle);
                    if (hr != 0)
                        throw new ClrDiagnosticsException($"Could not free the snapshot. Error {hr}.", ClrDiagnosticsExceptionKind.Unknown, hr);

                    try
                    {
                        Process.GetProcessById(_pid).Kill();
                    }
                    catch (Win32Exception)
                    {
                    }
                }

                if (_process != IntPtr.Zero)
                    CloseHandle(_process);

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~LiveDataReader()
        {
            Dispose(false);
        }

        public uint ProcessId => (uint)_pid;

        public bool IsThreadSafe => true;

        public bool IsFullMemoryAvailable => true;

        public void FlushCachedData()
        {
        }

        public Architecture Architecture => IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64;

        public int PointerSize => IntPtr.Size;

        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            List<ModuleInfo> result = new List<ModuleInfo>();

            EnumProcessModules(_process, null, 0, out uint needed);

            IntPtr[] modules = new IntPtr[needed / 4];
            uint size = (uint)modules.Length * sizeof(uint);

            if (!EnumProcessModules(_process, modules, size, out _))
                throw new ClrDiagnosticsException("Unable to get process modules.", ClrDiagnosticsExceptionKind.DataRequestError);

            for (int i = 0; i < modules.Length; i++)
            {
                IntPtr ptr = modules[i];

                if (ptr == IntPtr.Zero)
                {
                    break;
                }

                StringBuilder sb = new StringBuilder(1024);
                uint res = GetModuleFileNameExA(_process, ptr, sb, sb.Capacity);
                DebugOnly.Assert(res != 0);

                ulong baseAddr = (ulong)ptr.ToInt64();
                GetFileProperties(baseAddr, out int filesize, out int timestamp);

                string fileName = sb.ToString();
                ModuleInfo module = new ModuleInfo(this, baseAddr, filesize, timestamp, fileName);
                result.Add(module);
            }

            return result;
        }

        public void GetVersionInfo(ulong addr, out VersionInfo version)
        {
            StringBuilder fileName = new StringBuilder(1024);
            uint res = GetModuleFileNameExA(_process, addr.AsIntPtr(), fileName, fileName.Capacity);
            DebugOnly.Assert(res != 0);

            if (DataTarget.PlatformFunctions.GetFileVersion(fileName.ToString(), out int major, out int minor, out int revision, out int patch))
                version = new VersionInfo(major, minor, revision, patch);
            else
                version = default;
        }

        public bool Read(ulong address, Span<byte> buffer, out int bytesRead)
        {
            try
            {
                fixed (byte* ptr = buffer)
                {
                    int res = ReadProcessMemory(_process, address.AsIntPtr(), ptr, buffer.Length, out IntPtr read);
                    bytesRead = (int)read;
                    return res != 0;
                }
            }
            catch
            {
                bytesRead = 0;
                return false;
            }
        }

        public ulong ReadPointer(ulong address)
        {
            ReadPointer(address, out ulong value);
            return value;
        }

        public unsafe bool Read<T>(ulong address, out T value) where T : unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            if (Read(address, buffer, out int size) && size == sizeof(T))
            {
                value = Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(buffer));
                return true;
            }

            value = default;
            return false;
        }

        public T Read<T>(ulong address) where T : unmanaged
        {
            Read(address, out T value);
            return value;
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[IntPtr.Size];
            if (Read(address, buffer, out int size) && size == IntPtr.Size)
            {
                value = buffer.AsPointer();
                return true;
            }

            value = 0;
            return false;
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            using Process process = Process.GetProcessById(_pid);
            ProcessThreadCollection threads = process.Threads;
            for (int i = 0; i < threads.Count; i++)
                yield return (uint)threads[i].Id;
        }

        public bool QueryMemory(ulong address, out MemoryRegionInfo vq)
        {
            MEMORY_BASIC_INFORMATION mem = default;
            IntPtr ptr = address.AsIntPtr();

            int res = VirtualQueryEx(_process, ptr, ref mem, new IntPtr(Marshal.SizeOf(mem)));
            if (res == 0)
            {
                vq = default;
                return false;
            }

            vq = new MemoryRegionInfo(mem.BaseAddress, mem.Size);
            return true;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            using SafeWin32Handle thread = OpenThread(ThreadAccess.THREAD_ALL_ACCESS, true, threadID);
            if (thread.IsInvalid)
                return false;

            fixed (byte* ptr = context)
                return GetThreadContext(thread.DangerousGetHandle(), new IntPtr(ptr));
        }

        private void GetFileProperties(ulong moduleBase, out int filesize, out int timestamp)
        {
            filesize = 0;
            timestamp = 0;

            Span<byte> buffer = stackalloc byte[sizeof(uint)];

            if (Read(moduleBase + 0x3c, buffer, out int read) && read == buffer.Length)
            {
                uint sigOffset = buffer.AsUInt32();
                int sigLength = 4;

                if (Read(moduleBase + sigOffset, buffer, out read) && read == buffer.Length)
                {
                    uint header = buffer.AsUInt32();

                    // Ensure the module contains the magic "PE" value at the offset it says it does.  This check should
                    // never fail unless we have the wrong base address for CLR.
                    DebugOnly.Assert(header == 0x4550);
                    if (header == 0x4550)
                    {
                        const int timeDataOffset = 4;
                        const int imageSizeOffset = 0x4c;
                        if (Read(moduleBase + sigOffset + (ulong)sigLength + timeDataOffset, buffer, out read) && read == buffer.Length)
                            timestamp = buffer.AsInt32();

                        if (Read(moduleBase + sigOffset + (ulong)sigLength + imageSizeOffset, buffer, out read) && read == buffer.Length)
                            filesize = buffer.AsInt32();
                    }
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool EnumProcessModules(IntPtr hProcess, [Out] IntPtr[]? lphModule, uint cb, [MarshalAs(UnmanagedType.U4)] out uint lpcbNeeded);

        [DllImport("psapi.dll", SetLastError = true)]
        [PreserveSig]
        public static extern uint GetModuleFileNameExA([In] IntPtr hProcess, [In] IntPtr hModule, [Out] StringBuilder lpFilename, [In][MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte* lpBuffer,
            int dwSize,
            out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, ref MEMORY_BASIC_INFORMATION lpBuffer, IntPtr dwLength);

        [DllImport("kernel32.dll")]
        private static extern bool GetThreadContext(IntPtr hThread, IntPtr lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeWin32Handle OpenThread(ThreadAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32")]
        private static extern int PssCaptureSnapshot(IntPtr ProcessHandle, PSS_CAPTURE_FLAGS CaptureFlags, int ThreadContextFlags, out IntPtr SnapshotHandle);

        [DllImport("kernel32")]
        private static extern int PssFreeSnapshot(IntPtr ProcessHandle, IntPtr SnapshotHandle);

        [DllImport("kernel32")]
        private static extern int PssQuerySnapshot(IntPtr SnapshotHandle, PSS_QUERY_INFORMATION_CLASS InformationClass, out IntPtr Buffer, int BufferLength);

        [DllImport("kernel32")]
        private static extern int GetProcessId(IntPtr hObject);

        [DllImport("kernel32.dll")]
        internal static extern int ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int dwSize, out int lpNumberOfBytesRead);
    }
}