// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.DataReaders.Windows;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed unsafe class WindowsProcessDataReader : IDataReader, IDisposable
    {
        private bool _disposed = false;
        private readonly WindowsThreadSuspender? _suspension;
        private readonly int _originalPid;
        private readonly IntPtr _snapshotHandle;
        private readonly IntPtr _cloneHandle;
        private readonly IntPtr _process;
        private readonly int _pid;

        private const int PROCESS_VM_READ = 0x10;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;

        public string DisplayName => $"pid:{_pid:x}";
        public OSPlatform TargetPlatform => OSPlatform.Windows;

        public WindowsProcessDataReader(int processId, WindowsProcessDataReaderMode mode)
        {
            if (mode == WindowsProcessDataReaderMode.Snapshot)
            {
                _originalPid = processId;

                // Throws InvalidOperationException, which is similar to how Process.Start fails if it can't start the process.
                Process process = Process.GetProcessById(processId);
                int hr = PssCaptureSnapshot(process.Handle, PSS_CAPTURE_FLAGS.PSS_CAPTURE_VA_CLONE, IntPtr.Size == 8 ? 0x0010001F : 0x0001003F, out _snapshotHandle);
                if (hr != 0)
                    throw new InvalidOperationException($"Could not create snapshot to process. Error {hr}.");

                hr = PssQuerySnapshot(_snapshotHandle, PSS_QUERY_INFORMATION_CLASS.PSS_QUERY_VA_CLONE_INFORMATION, out _cloneHandle, IntPtr.Size);
                if (hr != 0)
                    throw new InvalidOperationException($"Could not create snapshot to process. Error {hr}.");

                _pid = GetProcessId(_cloneHandle);
            }
            else
            {
                _pid = processId;
            }

            _process = WindowsFunctions.NativeMethods.OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _pid);

            if (_process == IntPtr.Zero)
            {
                if (!WindowsFunctions.IsProcessRunning(_pid))
                    throw new ArgumentException($"Process {processId} is not running.");

                int hr = Marshal.GetLastWin32Error();
                throw new ArgumentException($"Could not attach to process {processId}, error: {hr:x}");
            }

            using Process p = Process.GetCurrentProcess();
            if (DataTarget.PlatformFunctions.TryGetWow64(p.Handle, out bool wow64)
                && DataTarget.PlatformFunctions.TryGetWow64(_process, out bool targetWow64)
                && wow64 != targetWow64)
            {
                throw new InvalidOperationException("Mismatched architecture between this process and the target process.");
            }

            if (mode == WindowsProcessDataReaderMode.Suspend)
                _suspension = new WindowsThreadSuspender(_pid);
        }

        private void Dispose(bool _)
        {
            if (!_disposed)
            {
                _suspension?.Dispose();

                if (_originalPid != 0)
                {
                    int hr = PssFreeSnapshot(Process.GetCurrentProcess().Handle, _snapshotHandle);
                    if (hr != 0)
                        throw new InvalidOperationException($"Could not free the snapshot. Error {hr}.");

                    try
                    {
                        Process.GetProcessById(_pid).Kill();
                    }
                    catch (Win32Exception)
                    {
                    }
                }

                if (_process != IntPtr.Zero)
                    WindowsFunctions.NativeMethods.CloseHandle(_process);

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~WindowsProcessDataReader()
        {
            Dispose(false);
        }

        public uint ProcessId => (uint)_pid;

        public bool IsThreadSafe => true;

        public void FlushCachedData()
        {
        }

        public Architecture Architecture => IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64;

        public int PointerSize => IntPtr.Size;

        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            EnumProcessModules(_process, null, 0, out uint needed);

            IntPtr[] modules = new IntPtr[needed / IntPtr.Size];

            if (!EnumProcessModules(_process, modules, needed, out _))
                throw new InvalidOperationException("Unable to get process modules.");

            List<ModuleInfo> result = new List<ModuleInfo>(modules.Length);

            for (int i = 0; i < modules.Length; i++)
            {
                IntPtr ptr = modules[i];

                StringBuilder sb = new StringBuilder(1024);
                uint res = GetModuleFileNameEx(_process, ptr, sb, sb.Capacity);
                DebugOnly.Assert(res != 0);

                ulong baseAddr = (ulong)ptr.ToInt64();
                GetFileProperties(baseAddr, out int filesize, out int timestamp);

                string fileName = sb.ToString();
                ModuleInfo module = new ModuleInfo(baseAddr, fileName, true, filesize, timestamp, ImmutableArray<byte>.Empty);
                result.Add(module);
            }

            return result;
        }

        public ImmutableArray<byte> GetBuildId(ulong baseAddress) => ImmutableArray<byte>.Empty;

        public bool GetVersionInfo(ulong addr, out VersionInfo version)
        {
            StringBuilder fileName = new StringBuilder(1024);
            uint res = GetModuleFileNameEx(_process, addr.AsIntPtr(), fileName, fileName.Capacity);
            DebugOnly.Assert(res != 0);

            if (DataTarget.PlatformFunctions.GetFileVersion(fileName.ToString(), out int major, out int minor, out int revision, out int patch))
            {
                version = new VersionInfo(major, minor, revision, patch, true);
                return true;
            }

            version = default;
            return false;
        }

        public bool Read(ulong address, Span<byte> buffer, out int bytesRead)
        {
            DebugOnly.Assert(!buffer.IsEmpty);
            try
            {
                fixed (byte* ptr = buffer)
                {
                    int res = ReadProcessMemory(_process, address.AsIntPtr(), ptr, new IntPtr(buffer.Length), out IntPtr read);
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

        private const string Kernel32LibraryName = "kernel32.dll";

        [DllImport(Kernel32LibraryName, SetLastError = true, EntryPoint = "K32EnumProcessModules")]
        public static extern bool EnumProcessModules(IntPtr hProcess, [Out] IntPtr[]? lphModule, uint cb, [MarshalAs(UnmanagedType.U4)] out uint lpcbNeeded);

        [DllImport(Kernel32LibraryName, CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "K32GetModuleFileNameExW")]
        public static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpFilename, [MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport(Kernel32LibraryName)]
        private static extern int ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte* lpBuffer,
            IntPtr dwSize,
            out IntPtr lpNumberOfBytesRead);

        [DllImport(Kernel32LibraryName, SetLastError = true)]
        internal static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, IntPtr dwLength);

        [DllImport(Kernel32LibraryName)]
        private static extern bool GetThreadContext(IntPtr hThread, IntPtr lpContext);

        [DllImport(Kernel32LibraryName, SetLastError = true)]
        internal static extern SafeWin32Handle OpenThread(ThreadAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwThreadId);
        
        [DllImport(Kernel32LibraryName, SetLastError = true)]
        internal static extern int SuspendThread(IntPtr hThread);

        [DllImport(Kernel32LibraryName, SetLastError = true)]
        internal static extern int ResumeThread(IntPtr hThread);

        [DllImport(Kernel32LibraryName)]
        private static extern int PssCaptureSnapshot(IntPtr ProcessHandle, PSS_CAPTURE_FLAGS CaptureFlags, int ThreadContextFlags, out IntPtr SnapshotHandle);

        [DllImport(Kernel32LibraryName)]
        private static extern int PssFreeSnapshot(IntPtr ProcessHandle, IntPtr SnapshotHandle);

        [DllImport(Kernel32LibraryName)]
        private static extern int PssQuerySnapshot(IntPtr SnapshotHandle, PSS_QUERY_INFORMATION_CLASS InformationClass, out IntPtr Buffer, int BufferLength);

        [DllImport(Kernel32LibraryName)]
        private static extern int GetProcessId(IntPtr hObject);
    }
}