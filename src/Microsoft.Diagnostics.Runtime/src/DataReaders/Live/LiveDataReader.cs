// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
    internal unsafe class LiveDataReader : IDataReader2
    {
        private readonly int _originalPid;
        private readonly IntPtr _snapshotHandle;
        private readonly IntPtr _cloneHandle;
        private IntPtr _process;
        private readonly int _pid;

        private const int PROCESS_VM_READ = 0x10;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;

        public LiveDataReader(int pid, bool createSnapshot)
        {
            if (createSnapshot)
            {
                _originalPid = pid;
                Process process = Process.GetProcessById(pid);
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
                _pid = pid;
            }

            _process = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _pid);

            if (_process == IntPtr.Zero)
            {
                int hr = Marshal.GetLastWin32Error();
                throw new ClrDiagnosticsException($"Could not attach to process. Error {hr}.", ClrDiagnosticsExceptionKind.Unknown, hr);
            }

            using (Process p = Process.GetCurrentProcess())
                if (DataTarget.PlatformFunctions.TryGetWow64(p.Handle, out bool wow64) &&
                    DataTarget.PlatformFunctions.TryGetWow64(_process, out bool targetWow64) &&
                    wow64 != targetWow64)
                {
                    throw new ClrDiagnosticsException("Dac architecture mismatch!", ClrDiagnosticsExceptionKind.DacError);
                }
        }

        public uint ProcessId => (uint)_pid;

        public bool IsMinidump => false;

        public void Close()
        {
            if (_originalPid != 0)
            {
                CloseHandle(_cloneHandle);
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
            {
                CloseHandle(_process);
                _process = IntPtr.Zero;
            }
        }

        public void Flush()
        {
        }

        public Architecture GetArchitecture()
        {
            if (IntPtr.Size == 4)
                return Architecture.X86;

            return Architecture.Amd64;
        }

        public uint GetPointerSize()
        {
            return (uint)IntPtr.Size;
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            List<ModuleInfo> result = new List<ModuleInfo>();

            EnumProcessModules(_process, null, 0, out uint needed);

            IntPtr[] modules = new IntPtr[needed / 4];
            uint size = (uint)modules.Length * sizeof(uint);

            if (!EnumProcessModules(_process, modules, size, out needed))
                throw new ClrDiagnosticsException("Unable to get process modules.", ClrDiagnosticsExceptionKind.DataRequestError);

            for (int i = 0; i < modules.Length; i++)
            {
                IntPtr ptr = modules[i];

                if (ptr == IntPtr.Zero)
                {
                    break;
                }

                StringBuilder sb = new StringBuilder(1024);
                GetModuleFileNameExA(_process, ptr, sb, sb.Capacity);

                ulong baseAddr = (ulong)ptr.ToInt64();
                GetFileProperties(baseAddr, out uint filesize, out uint timestamp);

                string filename = sb.ToString();
                ModuleInfo module = new ModuleInfo(this)
                {
                    ImageBase = baseAddr,
                    FileName = filename,
                    FileSize = filesize,
                    TimeStamp = timestamp
                };

                result.Add(module);
            }

            return result;
        }

        public void GetVersionInfo(ulong addr, out VersionInfo version)
        {
            StringBuilder filename = new StringBuilder(1024);
            GetModuleFileNameExA(_process, addr.AsIntPtr(), filename, filename.Capacity);

            if (DataTarget.PlatformFunctions.GetFileVersion(filename.ToString(), out int major, out int minor, out int revision, out int patch))
                version = new VersionInfo(major, minor, revision, patch);
            else
                version = new VersionInfo();
        }

        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            try
            {
                int res = ReadProcessMemory(_process, address.AsIntPtr(), buffer, bytesRequested, out bytesRead);
                return res != 0;
            }
            catch
            {
                bytesRead = 0;
                return false;
            }
        }

        public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            try
            {
                int res = ReadProcessMemory(_process, address.AsIntPtr(), buffer, bytesRequested, out bytesRead);
                return res != 0;
            }
            catch
            {
                bytesRead = 0;
                return false;
            }
        }

        private readonly byte[] _ptrBuffer = new byte[IntPtr.Size];

        public ulong ReadPointerUnsafe(ulong addr)
        {
            if (!ReadMemory(addr, _ptrBuffer, IntPtr.Size, out int read))
                return 0;

            fixed (byte* r = _ptrBuffer)
            {
                if (IntPtr.Size == 4)
                    return *((uint*)r);

                return *((ulong*)r);
            }
        }

        public uint ReadDwordUnsafe(ulong addr)
        {
            if (!ReadMemory(addr, _ptrBuffer, 4, out int read))
                return 0;

            fixed (byte* r = _ptrBuffer)
                return *((uint*)r);
        }

        public ulong GetThreadTeb(uint thread)
        {
            // todo
            throw new NotImplementedException();
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            Process p = Process.GetProcessById(_pid);
            foreach (ProcessThread thread in p.Threads)
                yield return (uint)thread.Id;
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            vq = new VirtualQueryData();

            MEMORY_BASIC_INFORMATION mem = new MEMORY_BASIC_INFORMATION();
            IntPtr ptr = addr.AsIntPtr();

            int res = VirtualQueryEx(_process, ptr, ref mem, new IntPtr(Marshal.SizeOf(mem)));
            if (res == 0)
                return false;

            vq.BaseAddress = mem.BaseAddress;
            vq.Size = mem.Size;
            return true;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            using (SafeWin32Handle thread = OpenThread(ThreadAccess.THREAD_ALL_ACCESS, true, threadID))
            {
                if (thread.IsInvalid)
                    return false;

                bool res = GetThreadContext(thread.DangerousGetHandle(), context);
                return res;
            }
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
        {
            using (SafeWin32Handle thread = OpenThread(ThreadAccess.THREAD_ALL_ACCESS, true, threadID))
            {
                if (thread.IsInvalid)
                    return false;

                fixed (byte* b = context)
                {
                    bool res = GetThreadContext(thread.DangerousGetHandle(), new IntPtr(b));
                    return res;
                }
            }
        }

        private void GetFileProperties(ulong moduleBase, out uint filesize, out uint timestamp)
        {
            filesize = 0;
            timestamp = 0;
            byte[] buffer = new byte[4];

            if (ReadMemory(moduleBase + 0x3c, buffer, buffer.Length, out int read) && read == buffer.Length)
            {
                uint sigOffset = (uint)BitConverter.ToInt32(buffer, 0);
                int sigLength = 4;

                if (ReadMemory(moduleBase + sigOffset, buffer, buffer.Length, out read) && read == buffer.Length)
                {
                    uint header = (uint)BitConverter.ToInt32(buffer, 0);

                    // Ensure the module contains the magic "PE" value at the offset it says it does.  This check should
                    // never fail unless we have the wrong base address for CLR.
                    Debug.Assert(header == 0x4550);
                    if (header == 0x4550)
                    {
                        const int timeDataOffset = 4;
                        const int imageSizeOffset = 0x4c;
                        if (ReadMemory(moduleBase + sigOffset + (ulong)sigLength + timeDataOffset, buffer, buffer.Length, out read) && read == buffer.Length)
                            timestamp = (uint)BitConverter.ToInt32(buffer, 0);

                        if (ReadMemory(moduleBase + sigOffset + (ulong)sigLength + imageSizeOffset, buffer, buffer.Length, out read) && read == buffer.Length)
                            filesize = (uint)BitConverter.ToInt32(buffer, 0);
                    }
                }
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "OpenProcess")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool EnumProcessModules(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, [MarshalAs(UnmanagedType.U4)] out uint lpcbNeeded);

        [DllImport("psapi.dll", SetLastError = true)]
        [PreserveSig]
        public static extern uint GetModuleFileNameExA([In] IntPtr hProcess, [In] IntPtr hModule, [Out] StringBuilder lpFilename, [In][MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("kernel32.dll")]
        private static extern int ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            byte[] lpBuffer,
            int dwSize,
            out int lpNumberOfBytesRead);

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