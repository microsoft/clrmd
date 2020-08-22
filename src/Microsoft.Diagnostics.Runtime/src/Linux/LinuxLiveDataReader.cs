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
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;
using ProcessArchitecture = System.Runtime.InteropServices.Architecture;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    /// <summary>
    /// A data reader that targets a Linux process.
    /// The current process must have ptrace access to the target process.
    /// </summary>
    internal sealed class LinuxLiveDataReader : CommonMemoryReader, IDataReader, IDisposable, IThreadReader
    {
        private ImmutableArray<MemoryMapEntry>.Builder _memoryMapEntries;
        private readonly List<uint> _threadIDs = new();

        private bool _suspended;
        private bool _disposed;

        public string DisplayName => $"pid:{ProcessId:x}";
        public OSPlatform TargetPlatform => OSPlatform.Linux;

        public LinuxLiveDataReader(int processId, bool suspend)
        {
            int status = kill(processId, 0);
            if (status < 0 && Marshal.GetLastWin32Error() != EPERM)
                throw new ArgumentException("The process is not running");

            ProcessId = processId;
            _memoryMapEntries = LoadMemoryMaps();

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

        public int ProcessId { get; }

        public bool IsThreadSafe => false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool _)
        {
            if (_disposed)
                return;

            if (_suspended)
            {
                int status = (int)ptrace(PTRACE_DETACH, ProcessId, IntPtr.Zero, IntPtr.Zero);
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
            _memoryMapEntries = LoadMemoryMaps();
        }

        public Architecture Architecture { get; }

        public IEnumerable<ModuleInfo> EnumerateModules() =>
            from entry in _memoryMapEntries
            where !string.IsNullOrEmpty(entry.FilePath)
            group entry by entry.FilePath into image
            let filePath = image.Key
            let containsExecutable = image.Any(entry => entry.IsExecutable)
            let beginAddress = image.Min(entry => entry.BeginAddress)
            let props = GetPEImageProperties(filePath)
            select new ModuleInfo(this, beginAddress, filePath, containsExecutable, props.Filesize, props.Timestamp, buildId: default);

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
                return this.GetVersionInfo(baseAddress, file, out version);
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

        public override int Read(ulong address, Span<byte> buffer)
        {
            DebugOnly.Assert(!buffer.IsEmpty);
            return ReadMemoryReadv(address, buffer);
        }

        private unsafe int ReadMemoryReadv(ulong address, Span<byte> buffer)
        {
            int readableBytesCount = this.GetReadableBytesCount(this._memoryMapEntries, address, buffer.Length);
            if (readableBytesCount <= 0)
            {
                return 0;
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
                int read = (int)process_vm_readv(ProcessId, &local, (UIntPtr)1, &remote, (UIntPtr)1, UIntPtr.Zero).ToInt64();
                if (read < 0)
                {
                    return Marshal.GetLastWin32Error() switch
                    {
                        EPERM => throw new UnauthorizedAccessException(),
                        ESRCH => throw new InvalidOperationException("The process has exited"),
                        _ => 0
                    };
                }

                return read;
            }
        }

        public IEnumerable<uint> EnumerateOSThreadIds()
        {
            LoadThreads();
            return _threadIDs;
        }

        public ulong GetThreadTeb(uint _) => 0;

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

        private ImmutableArray<MemoryMapEntry>.Builder LoadMemoryMaps()
        {
            ImmutableArray<MemoryMapEntry>.Builder result = ImmutableArray.CreateBuilder<MemoryMapEntry>();
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
        private static extern IntPtr ptrace(int request, int pid, IntPtr addr, IntPtr data);

        [DllImport(LibC, SetLastError = true)]
        private static extern unsafe IntPtr process_vm_readv(int pid, iovec* local_iov, UIntPtr liovcnt, iovec* remote_iov, UIntPtr riovcnt, UIntPtr flags);

        [DllImport(LibC)]
        private static extern int waitpid(int pid, IntPtr status, int options);

        private unsafe struct iovec
        {
            public void* iov_base;
            public IntPtr iov_len;
        }

        private const int PTRACE_GETREGS = 12;
        private const int PTRACE_ATTACH = 16;
        private const int PTRACE_DETACH = 17;
    }

    internal struct MemoryMapEntry : IRegion
    {
        public ulong BeginAddress { get; set; }
        public ulong EndAddress { get; set; }
        public string? FilePath { get; set; }
        public int Permission { get; set; }

        public bool IsReadable => (Permission & 8) != 0;

        public bool IsExecutable => (Permission & 2) != 0;
    }
}