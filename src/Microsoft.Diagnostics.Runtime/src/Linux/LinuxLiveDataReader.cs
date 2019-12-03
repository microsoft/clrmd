// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    /// <summary>
    /// A data reader targets a Linux process, implemented by reading /proc/[pid]/maps
    /// and /proc/[pid]/mem files. The process must have READ permission to the above 2
    /// files.
    ///   1. The current process can run as root.
    ///   2. If executed from within a Docker container, the best way is to use "ptrace
    ///      attach" to obtain the permission.
    ///        - the container should be started with "--cap-add=SYS_PTRACE" or equivalent.
    ///        - the process must call the following before constructing the data reader.
    ///             if (ptrace(PTRACE_ATTACH, targetProcessId, NULL, NULL) != 0) { fail }
    ///             wait(NULL);
    /// </summary>
    internal class LinuxLiveDataReader : IDataReader
    {
        private List<MemoryMapEntry> _memoryMapEntries;
        private readonly List<uint> _threadIDs = new List<uint>();

        private bool _suspended;
        private bool _disposed;

        public LinuxLiveDataReader(int processId, bool suspend)
        {
            ProcessId = (uint)processId;
            _memoryMapEntries = LoadMemoryMap();

            if (suspend)
            {
                int status = (int)ptrace(PTRACE_ATTACH, processId, IntPtr.Zero, IntPtr.Zero);

                if (status >= 0)
                    status = waitpid(processId, IntPtr.Zero, 0);

                if (status < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    throw new ClrDiagnosticsException($"Could not attach to pid {processId}, errno: {errno}", ClrDiagnosticsExceptionKind.DebuggerError, errno);
                }

                _suspended = true;
            }
        }

        ~LinuxLiveDataReader() => Dispose(false);

        public uint ProcessId { get; private set; }

        public bool IsMinidump => false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (_suspended)
            {
                int status = (int)ptrace(PTRACE_DETACH, (int)ProcessId, IntPtr.Zero, IntPtr.Zero);
                if (status < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    throw new ClrDiagnosticsException($"Could not detach from pid {ProcessId}, errno: {errno}", ClrDiagnosticsExceptionKind.DebuggerError, errno);
                }

                _suspended = false;
            }

            _disposed = true;
        }

        public void ClearCachedData()
        {
            _threadIDs.Clear();
            _memoryMapEntries = LoadMemoryMap();
        }

        public Architecture Architecture => IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64;
        public int PointerSize => IntPtr.Size;

        public IList<ModuleInfo> EnumerateModules()
        {
            List<ModuleInfo> result = new List<ModuleInfo>();
            foreach (var entry in _memoryMapEntries)
            {
                if (string.IsNullOrEmpty(entry.FilePath))
                {
                    continue;
                }
                if (!result.Exists(module => module.FileName == entry.FilePath))
                {
                    uint filesize = 0;
                    uint timestamp = 0;

                    if (File.Exists(entry.FilePath))
                    {
                        var fileInfo = new FileInfo(entry.FilePath);
                        filesize = (uint)fileInfo.Length;
                        timestamp = (uint)new DateTimeOffset(fileInfo.CreationTimeUtc).ToUnixTimeSeconds();
                    }

                    ModuleInfo moduleInfo = new ModuleInfo(this, entry.BeginAddr, filesize, timestamp, entry.FilePath);
                    result.Add(moduleInfo);
                }
            }
            return result;
        }

        public void GetVersionInfo(ulong addr, out VersionInfo version)
        {
            version = default;
        }

        public bool ReadMemory(ulong address, Span<byte> span, out int bytesRead)
        {
            return ReadMemoryReadv(address, span, out bytesRead);
        }

        private unsafe bool ReadMemoryReadv(ulong address, Span<byte> buffer, out int bytesRead)
        {
            bytesRead = 0;
            int readableBytesCount = GetReadableBytesCount(address, buffer.Length);
            if (readableBytesCount <= 0)
            {
                return false;
            }

            fixed (byte* ptr = buffer)
            {
                var local = new iovec
                {
                    iov_base = ptr,
                    iov_len = (IntPtr)readableBytesCount
                };
                var remote = new iovec
                {
                    iov_base = (void*)address,
                    iov_len = (IntPtr)readableBytesCount
                };
                int read = (int)process_vm_readv((int)ProcessId, &local, (UIntPtr)1, &remote, (UIntPtr)1, UIntPtr.Zero).ToInt64();
                if (read < 0)
                {
                    bytesRead = 0;
                    return (Marshal.GetLastWin32Error()) switch
                    {
                        EPERM => throw new UnauthorizedAccessException(),
                        ESRCH => throw new InvalidOperationException("The process has exited"),
                        _ => false
                    };
                }

                bytesRead = read;
                return true;
            }
        }

        public ulong ReadPointerUnsafe(ulong address)
        {
            ReadPointer(address, out ulong value);
            return value;
        }


        public unsafe bool Read<T>(ulong addr, out T value) where T : unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            if (!ReadMemory(addr, buffer, out _))
            {
                value = Unsafe.As<byte, T>(ref buffer[0]);
                return true;
            }

            value = default;
            return false;
        }

        public T ReadUnsafe<T>(ulong addr) where T : unmanaged
        {
            Read(addr, out T value);
            return value;
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[IntPtr.Size];
            if (!ReadMemory(address, buffer, out _))
            {
                value = buffer.AsPointer();
                return true;
            }

            value = 0;
            return false;
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            LoadThreads();
            return _threadIDs;
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            foreach (var entry in _memoryMapEntries)
            {
                if (entry.BeginAddr <= addr && entry.EndAddr >= addr)
                {
                    vq = new VirtualQueryData(entry.BeginAddr, entry.EndAddr - entry.BeginAddr + 1);
                    return true;
                }
            }
            vq = default;
            return false;
        }

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            LoadThreads();
            if (!_threadIDs.Contains(threadID) || context.Length != AMD64Context.Size)
                return false;

            ref AMD64Context ctx = ref Unsafe.As<byte, AMD64Context>(ref MemoryMarshal.GetReference(context));
            ctx.ContextFlags = contextFlags;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(sizeof(RegSetX64));
            try
            {
                fixed (byte* data = buffer)
                {
                    ptrace(PTRACE_GETREGS, (int)threadID, IntPtr.Zero, new IntPtr(data));
                }

                CopyContext(ref ctx, Unsafe.As<byte, RegSetX64>(ref MemoryMarshal.GetReference(buffer.AsSpan())));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return true;
        }

        internal IEnumerable<string> GetModulesFullPath()
        {
            return _memoryMapEntries.Where(e => !string.IsNullOrEmpty(e.FilePath)).Select(e => e.FilePath).Distinct();
        }

        private static void CopyContext(ref AMD64Context context, in RegSetX64 registerSet)
        {
            context.R15 = registerSet.R15;
            context.R14 = registerSet.R14;
            context.R13 = registerSet.R13;
            context.R12 = registerSet.R12;
            context.Rbp = registerSet.Rbp;
            context.Rbx = registerSet.Rbx;
            context.R11 = registerSet.R11;
            context.R10 = registerSet.R10;
            context.R9 = registerSet.R9;
            context.R8 = registerSet.R8;
            context.Rax = registerSet.Rax;
            context.Rcx = registerSet.Rcx;
            context.Rdx = registerSet.Rdx;
            context.Rsi = registerSet.Rsi;
            context.Rdi = registerSet.Rdi;
            context.Rip = registerSet.Rip;
            context.Rsp = registerSet.Rsp;
        }

        private void LoadThreads()
        {
            if (_threadIDs.Count == 0)
            {
                string taskDirPath = $"/proc/{ProcessId}/task";
                foreach (var taskDir in Directory.GetDirectories(taskDirPath))
                {
                    string dirName = Path.GetFileName(taskDir);
                    if (uint.TryParse(dirName, out uint taskId) && taskId > 0)
                    {
                        _threadIDs.Add(taskId);
                    }
                }
            }
        }

        private int GetReadableBytesCount(ulong address, int bytesRequested)
        {
            if (bytesRequested < 1)
            {
                return 0;
            }
            ulong endAddress = address + (ulong)bytesRequested - 1;
            int startIndex = -1;
            for (int i = 0; i < _memoryMapEntries.Count; i++)
            {
                var entry = _memoryMapEntries[i];
                if (entry.BeginAddr <= address && address < entry.EndAddr && entry.IsReadable())
                {
                    startIndex = i;
                }
            }
            if (startIndex < 0)
            {
                return 0;
            }
            int endIndex = _memoryMapEntries.Count - 1;
            for (int i = startIndex; i < _memoryMapEntries.Count; i++)
            {
                var entry = _memoryMapEntries[i];
                if (!entry.IsReadable()         // the current region is not readable
                    || entry.BeginAddr > endAddress
                    || (i > startIndex && _memoryMapEntries[i - 1].EndAddr != entry.BeginAddr))   // the region is no longer continuous
                {
                    endIndex = i - 1;
                    break;
                }
            }
            int readableBytesCount = 0;
            ulong offset = address - _memoryMapEntries[startIndex].BeginAddr;
            for (int i = startIndex; i <= endIndex; i++)
            {
                var entry = _memoryMapEntries[i];
                ulong regionSize = entry.EndAddr - entry.BeginAddr - offset;
                if (regionSize >= (ulong)bytesRequested)
                {
                    readableBytesCount += bytesRequested;
                    break;
                }
                else
                {
                    bytesRequested -= (int)regionSize;
                    readableBytesCount += (int)regionSize;
                }
                offset = 0;
            }
            return readableBytesCount;
        }

        private List<MemoryMapEntry> LoadMemoryMap()
        {
            List<MemoryMapEntry> result = new List<MemoryMapEntry>();
            string mapsFilePath = $"/proc/{ProcessId}/maps";
            using StreamReader reader = new StreamReader(mapsFilePath);
            while (true)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }
                string address, permission, path;
                string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 5)
                {
                    path = string.Empty;
                }
                else if (parts.Length == 6)
                {
                    path = parts[5].StartsWith("[") ? string.Empty : parts[5];
                }
                else
                {
                    // Unknown data format
                    continue;
                }
                address = parts[0];
                permission = parts[1];
                string[] addressBeginEnd = address.Split('-');
                MemoryMapEntry entry = new MemoryMapEntry()
                {
                    BeginAddr = Convert.ToUInt64(addressBeginEnd[0], 16),
                    EndAddr = Convert.ToUInt64(addressBeginEnd[1], 16),
                    FilePath = path,
                    Permission = ParsePermission(permission)
                };
                result.Add(entry);
            }

            return result;
        }

        private static int ParsePermission(string permission)
        {
            // parse something like rwxp or r-xp. more info see
            // https://stackoverflow.com/questions/1401359/understanding-linux-proc-id-maps
            Debug.Assert(permission.Length == 4);

            int r = permission[0] != '-' ? 8 : 0;   // 8: can read
            int w = permission[1] != '-' ? 4 : 0;   // 4: can write
            int x = permission[2] != '-' ? 2 : 0;   // 2: can execute
            int p = permission[3] != '-' ? 1 : 0;   // 1: private
            return r | w | x | p;
        }

        private const int EPERM = 1;
        private const int ESRCH = 3;

        private const string LibC = "libc";

        [DllImport(LibC, SetLastError = true)]
        private static extern IntPtr ptrace(uint request, int pid, IntPtr addr, IntPtr data);

        [DllImport(LibC, SetLastError = true)]
        private static extern unsafe IntPtr process_vm_readv(int pid, iovec* local_iov, UIntPtr liovcnt, iovec* remote_iov, UIntPtr riovcnt, UIntPtr flags);

        [DllImport(LibC)]
        private static extern int waitpid(int pid, IntPtr status, int options);

        private unsafe struct iovec
        {
            public void* iov_base;
            public IntPtr iov_len;
        }

        private const uint PTRACE_GETREGS = 12;
        private const uint PTRACE_ATTACH = 16;
        private const uint PTRACE_DETACH = 17;
    }

    internal class MemoryMapEntry
    {
        public ulong BeginAddr { get; set; }
        public ulong EndAddr { get; set; }
        public string FilePath { get; set; }
        public int Permission { get; set; }

        public bool IsReadable()
        {
            return (Permission & 8) != 0;
        }
    }
}