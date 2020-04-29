// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;
using ProcessArchitecture = System.Runtime.InteropServices.Architecture;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    /// <summary>
    /// A data reader that targets a Linux process.
    /// The current process must have ptrace access to the target process.
    /// </summary>
    internal class LinuxLiveDataReader : IDataReader, IDisposable
    {
        private List<MemoryMapEntry> _memoryMapEntries;
        private readonly List<uint> _threadIDs = new List<uint>();

        private bool _suspended;
        private bool _disposed;

        public string DisplayName => $"pid:{ProcessId:x}";
        public OSPlatform TargetPlatform => OSPlatform.Linux;

        public LinuxLiveDataReader(int processId, bool suspend)
        {
            int status = kill(processId, 0);
            if (status < 0 && Marshal.GetLastWin32Error() != EPERM)
                throw new ArgumentException("The process is not running");

            ProcessId = (uint)processId;
            _memoryMapEntries = LoadMemoryMap();

            if (suspend)
            {
                status = (int)ptrace(PTRACE_ATTACH, processId, IntPtr.Zero, IntPtr.Zero);

                if (status >= 0)
                    status = waitpid(processId, IntPtr.Zero, 0);

                if (status < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    throw new ClrDiagnosticsException($"Could not attach to process {processId}, errno: {errno}", errno);
                }

                _suspended = true;
            }

            Architecture = RuntimeInformation.ProcessArchitecture switch
            {
                ProcessArchitecture.X86 => Architecture.X86,
                ProcessArchitecture.X64 => Architecture.Amd64,
                ProcessArchitecture.Arm => Architecture.Arm,
                ProcessArchitecture.Arm64 => Architecture.Arm64,
                _ => Architecture.Unknown,
            };
        }

        ~LinuxLiveDataReader() => Dispose(false);

        public uint ProcessId { get; private set; }

        public bool IsThreadSafe => false;

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
                    throw new ClrDiagnosticsException($"Could not detach from process {ProcessId}, errno: {errno}", errno);
                }

                _suspended = false;
            }

            _disposed = true;
        }

        public void FlushCachedData()
        {
            _threadIDs.Clear();
            _memoryMapEntries = LoadMemoryMap();
        }

        public Architecture Architecture { get; }

        public int PointerSize => IntPtr.Size;

        public IEnumerable<ModuleInfo> EnumerateModules() =>
            from entry in _memoryMapEntries
            where !string.IsNullOrEmpty(entry.FilePath)
            group entry by entry.FilePath into image
            let filePath = image.Key
            let containsExecutable = image.Any(entry => entry.IsExecutable)
            let beginAddress = image.Min(entry => entry.BeginAddress)
            let props = GetPEImageProperties(filePath)
            select new ModuleInfo(beginAddress, filePath, containsExecutable, props.Filesize, props.Timestamp, buildId: default);

        private static (int Filesize, int Timestamp) GetPEImageProperties(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    using PEImage pe = new PEImage(File.OpenRead(filePath));
                    if (pe.IsValid)
                        return (pe.IndexFileSize, pe.IndexTimeStamp);
                }
                catch
                {
                }
            }

            return (0, 0);
        }

        public ImmutableArray<byte> GetBuildId(ulong baseAddress) => GetElfFile(baseAddress)?.BuildId ?? ImmutableArray<byte>.Empty;

        public unsafe bool GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            ElfFile? file = GetElfFile(baseAddress);
            if (file is null)
            {
                version = default;
                return false;
            }
            else
            {
                return LinuxFunctions.GetVersionInfo(this, baseAddress, file, out version);
            }
        }

        private ElfFile? GetElfFile(ulong baseAddress)
        {
            MemoryVirtualAddressSpace memoryAddressSpace = new MemoryVirtualAddressSpace(this);
            try
            {
                return new ElfFile(new Reader(memoryAddressSpace), (long)baseAddress);
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        public bool Read(ulong address, Span<byte> buffer, out int bytesRead)
        {
            DebugOnly.Assert(!buffer.IsEmpty);
            return ReadMemoryReadv(address, buffer, out bytesRead);
        }

        private unsafe bool ReadMemoryReadv(ulong address, Span<byte> buffer, out int bytesRead)
        {
            int readableBytesCount = GetReadableBytesCount(address, buffer.Length);
            if (readableBytesCount <= 0)
            {
                bytesRead = 0;
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
                    return Marshal.GetLastWin32Error() switch
                    {
                        EPERM => throw new UnauthorizedAccessException(),
                        ESRCH => throw new InvalidOperationException("The process has exited"),
                        _ => false
                    };
                }

                bytesRead = read;
                return read > 0;
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

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            LoadThreads();
            if (!_threadIDs.Contains(threadID) || Architecture == Architecture.X86)
                return false;

            int regSize = Architecture switch
            {
                Architecture.Arm => sizeof(RegSetArm),
                Architecture.Arm64 => sizeof(RegSetArm64),
                Architecture.Amd64 => sizeof(RegSetX64),
                _ => sizeof(RegSetX86),
            };

            byte[] buffer = ArrayPool<byte>.Shared.Rent(regSize);
            try
            {
                fixed (byte* data = buffer)
                {
                    ptrace(PTRACE_GETREGS, (int)threadID, IntPtr.Zero, new IntPtr(data));
                }

                switch (Architecture)
                {
                    case Architecture.Arm:
                        Unsafe.As<byte, RegSetArm>(ref MemoryMarshal.GetReference(buffer.AsSpan())).CopyContext(context);
                        break;
                    case Architecture.Arm64:
                        Unsafe.As<byte, RegSetArm64>(ref MemoryMarshal.GetReference(buffer.AsSpan())).CopyContext(context);
                        break;
                    case Architecture.Amd64:
                        Unsafe.As<byte, RegSetX64>(ref MemoryMarshal.GetReference(buffer.AsSpan())).CopyContext(context);
                        break;
                    default:
                        Unsafe.As<byte, RegSetX86>(ref MemoryMarshal.GetReference(buffer.AsSpan())).CopyContext(context);
                        break;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return true;
        }

        private void LoadThreads()
        {
            if (_threadIDs.Count == 0)
            {
                string taskDirPath = $"/proc/{ProcessId}/task";
                foreach (string taskDir in Directory.EnumerateDirectories(taskDirPath))
                {
                    string dirName = Path.GetFileName(taskDir);
                    if (uint.TryParse(dirName, out uint taskId))
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
            int bytesReadable = 0;
            ulong prevEndAddr = default;

            int startIndex = -1;
            for (int i = 0; i < _memoryMapEntries.Count; i++)
            {
                MemoryMapEntry entry = _memoryMapEntries[i];
                ulong entryBeginAddr = entry.BeginAddress;
                ulong entryEndAddr = entry.EndAddress;
                if (entryBeginAddr <= address && address < entryEndAddr && entry.IsReadable)
                {
                    int regionSize = (int)(entryEndAddr - address);
                    if (regionSize >= bytesRequested)
                    {
                        return bytesRequested;
                    }

                    startIndex = i;
                    bytesRequested -= regionSize;
                    bytesReadable = regionSize;
                    prevEndAddr = entryEndAddr;
                    break;
                }
            }

            if (startIndex < 0)
            {
                return 0;
            }

            for (int i = startIndex + 1; i < _memoryMapEntries.Count; i++)
            {
                MemoryMapEntry entry = _memoryMapEntries[i];
                ulong entryBeginAddr = entry.BeginAddress;
                ulong entryEndAddr = entry.EndAddress;
                if (entryBeginAddr > endAddress || entryBeginAddr != prevEndAddr || !entry.IsReadable)
                {
                    break;
                }

                int regionSize = (int)(entryEndAddr - entryBeginAddr);
                if (regionSize >= bytesRequested)
                {
                    bytesReadable += bytesRequested;
                    break;
                }

                bytesRequested -= regionSize;
                bytesReadable += regionSize;
                prevEndAddr = entryEndAddr;
            }

            return bytesReadable;
        }

        private List<MemoryMapEntry> LoadMemoryMap()
        {
            List<MemoryMapEntry> result = new List<MemoryMapEntry>();
            string mapsFilePath = $"/proc/{ProcessId}/maps";
            using StreamReader reader = new StreamReader(mapsFilePath);
            while (true)
            {
                string? line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                string address, permission, path;
                string[] parts = line.Split(new char[] { ' ' }, 6, StringSplitOptions.RemoveEmptyEntries);
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
                    DebugOnly.Fail("Unknown data format");
                    continue;
                }

                address = parts[0];
                permission = parts[1];
                string[] addressBeginEnd = address.Split('-');
                MemoryMapEntry entry = new MemoryMapEntry()
                {
                    BeginAddress = Convert.ToUInt64(addressBeginEnd[0], 16),
                    EndAddress = Convert.ToUInt64(addressBeginEnd[1], 16),
                    FilePath = path,
                    Permission = ParsePermission(permission)
                };
                result.Add(entry);
            }

            return result;
        }

        private static int ParsePermission(string permission)
        {
            DebugOnly.Assert(permission.Length == 4);

            // r = read
            // w = write
            // x = execute
            // s = shared
            // p = private (copy on write)
            int r = permission[0] == 'r' ? 8 : 0;
            int w = permission[1] == 'w' ? 4 : 0;
            int x = permission[2] == 'x' ? 2 : 0;
            int p = permission[3] == 'p' ? 1 : 0;
            return r | w | x | p;
        }

        private const int EPERM = 1;
        private const int ESRCH = 3;

        private const string LibC = "libc";

        [DllImport(LibC, SetLastError = true)]
        private static extern int kill(int pid, int sig);

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
        public ulong BeginAddress { get; set; }
        public ulong EndAddress { get; set; }
        public string? FilePath { get; set; }
        public int Permission { get; set; }

        public bool IsReadable => (Permission & 8) != 0;

        public bool IsExecutable => (Permission & 2) != 0;
    }
}