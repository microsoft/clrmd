// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#if !NET45
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    /// <summary>
    /// A data reader targets a Linux process, implemented by reading /proc/<pid>/maps 
    /// and /proc/<pid>/mem files. The process must have READ permission to the above 2
    /// files. 
    ///   1. The current process can run as root.
    ///   2. If executed from within a Docker container, the best way is to use "ptrace 
    ///      attach" to obtain the permission. 
    ///        - the container should be started with "--cap-add=SYS_PTRACE" or equivalent. 
    ///        - the process must call the following before constructing the data reader.
    ///             if (ptrace(PTRACE_ATTACH, targetProcessId, NULL, NULL) != 0) { fail }
    ///             wait(NULL);
    /// </summary>
    internal class LinuxLiveDataReader : IDataReader2
    {
        private List<MemoryMapEntry> _memoryMapEntries;
        private FileStream _memoryStream;
        private bool _initializedMemFile;
        private List<uint> _threadIDs = new List<uint>();
        private byte[] _ptrBuffer = new byte[IntPtr.Size];
        private byte[] _dwordBuffer = new byte[4];

        public LinuxLiveDataReader(uint processId)
        {
            this.ProcessId = processId;
            _memoryMapEntries = this.LoadMemoryMap();
        }

        public uint ProcessId { get; private set; }

        public bool IsMinidump { get { return false; } }

        public void Close()
        {
            _memoryStream?.Dispose();
            _memoryStream = null;
            _initializedMemFile = false;
        }

        public void Flush()
        {
            _threadIDs.Clear();
            _memoryStream?.Dispose();
            _memoryStream = null;
            _initializedMemFile = false;
            _memoryMapEntries = this.LoadMemoryMap();
        }

        public Architecture GetArchitecture()
        {
            return IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64;
        }

        public uint GetPointerSize()
        {
            return (uint)IntPtr.Size;
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            List<ModuleInfo> result = new List<ModuleInfo>();
            foreach (var entry in _memoryMapEntries)
            {
                if (string.IsNullOrEmpty(entry.FilePath))
                {
                    continue;
                }
                var module = result.FirstOrDefault(m => m.FileName == entry.FilePath);
                if (module == null)
                {
                    uint filesize = 0;
                    uint timestamp = 0;
                    VersionInfo? version = null;

                    if (File.Exists(entry.FilePath))
                    {
                        try
                        {
                            using FileStream stream = File.OpenRead(entry.FilePath);
                            PEImage pe = new PEImage(stream);
                            if (pe.IsValid)
                            {
                                filesize = (uint)pe.IndexFileSize;
                                timestamp = (uint)pe.IndexTimeStamp;
                                version = pe.GetFileVersionInfo()?.VersionInfo;
                            }
                        }
                        catch
                        {
                        }
                    }
                
                    ModuleInfo moduleInfo = new ModuleInfo(this, version)
                    {
                        ImageBase = entry.BeginAddr,
                        FileName = entry.FilePath,
                        FileSize = filesize,
                        TimeStamp = timestamp,
                    };
                    result.Add(moduleInfo);
                }
            }
            return result;
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            MemoryVirtualAddressSpace memoryAddressSpace = new MemoryVirtualAddressSpace(this);
            ElfFile file = new ElfFile(new Reader(memoryAddressSpace), (long)baseAddress);
            LinuxFunctions.GetVersionInfo(this, baseAddress, file, out version);
        }
        
        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            this.OpenMemFile();
            if (_memoryStream != null)
            {
                return ReadMemoryProcMem(address, buffer, bytesRequested, out bytesRead);
            }
            else
            {
                return ReadMemoryReadv(address, buffer, bytesRequested, out bytesRead);
            }
        }

        public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            this.OpenMemFile();
            if (_memoryStream != null)
            {
                return ReadMemoryProcMem(address, buffer, bytesRequested, out bytesRead);
            }
            else
            {
                return ReadMemoryReadv(address, buffer, bytesRequested, out bytesRead);
            }
        }

        private bool ReadMemoryProcMem(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            bytesRead = 0;
            int readableBytesCount = this.GetReadableBytesCount(address, bytesRequested);
            if (readableBytesCount <= 0)
            {
                bytesRead = 0;
                return false;
            }
            try
            {
                _memoryStream.Seek((long)address, SeekOrigin.Begin);
                bytesRead = _memoryStream.Read(buffer, 0, readableBytesCount);
                return bytesRead > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool ReadMemoryProcMem(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            bytesRead = 0;
            int readableBytesCount = this.GetReadableBytesCount(address, bytesRequested);
            if (readableBytesCount <= 0)
            {
                return false;
            }
            try
            {
                byte[] bytes = new byte[readableBytesCount];
                _memoryStream.Seek((long)address, SeekOrigin.Begin);
                bytesRead = _memoryStream.Read(bytes, 0, readableBytesCount);
                if (bytesRead > 0)
                {
                    Marshal.Copy(bytes, 0, buffer, bytesRead);
                }
                return bytesRead > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private unsafe bool ReadMemoryReadv(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            fixed (void* p = buffer)
            {
                return ReadMemoryReadv(address, (IntPtr)p, bytesRequested, out bytesRead);
            }
        }

        private unsafe bool ReadMemoryReadv(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            bytesRead = 0;
            int readableBytesCount = this.GetReadableBytesCount(address, bytesRequested);
            if (readableBytesCount <= 0)
            {
                return false;
            }

            var local = new iovec
            {
                iov_base = (void*)buffer,
                iov_len = (IntPtr)readableBytesCount
            };
            var remote = new iovec
            {
                iov_base = (void*)address,
                iov_len = (IntPtr)readableBytesCount
            };
            bytesRead = (int)process_vm_readv((int)ProcessId, &local, (UIntPtr)1, &remote, (UIntPtr)1, UIntPtr.Zero).ToInt64();
            return bytesRead > 0;
        }

        public ulong ReadPointerUnsafe(ulong address)
        {
            if (!ReadMemory(address, _ptrBuffer, IntPtr.Size, out int read))
            {
                return 0;
            }
            return IntPtr.Size == 4 ? BitConverter.ToUInt32(_ptrBuffer, 0) : BitConverter.ToUInt64(_ptrBuffer, 0);
        }

        public uint ReadDwordUnsafe(ulong address)
        {
            if (!ReadMemory(address, _dwordBuffer, 4, out int read))
            {
                return 0;
            }
            return BitConverter.ToUInt32(_dwordBuffer, 0);
        }

        public ulong GetThreadTeb(uint thread)
        {
            // not implemented
            return 0;
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            this.LoadThreads();
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
            vq = new VirtualQueryData();
            return false;
        }

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            this.LoadThreads();
            if (!_threadIDs.Contains(threadID) || contextSize != AMD64Context.Size)
            {
                return false;
            }
            AMD64Context* ctx = (AMD64Context*)context.ToPointer();
            ctx->ContextFlags = contextFlags;
            IntPtr ptr = Marshal.AllocHGlobal(sizeof(RegSetX64));
            try
            {
                ptrace(PTRACE_GETREGS, (int) threadID, IntPtr.Zero, ptr);
                RegSetX64 r = Marshal.PtrToStructure<RegSetX64>(ptr);
                CopyContext(ctx, ref r);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return true;
        }

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
        {
            this.LoadThreads();
            if (!_threadIDs.Contains(threadID) || contextSize != AMD64Context.Size)
            {
                return false;
            }
            IntPtr ptrContext = Marshal.AllocHGlobal(sizeof(AMD64Context));
            AMD64Context* ctx = (AMD64Context*)ptrContext;
            ctx->ContextFlags = contextFlags;
            IntPtr ptr = Marshal.AllocHGlobal(sizeof(RegSetX64));
            try
            {
                ptrace(PTRACE_GETREGS, (int)threadID, IntPtr.Zero, ptr);
                RegSetX64 r = Marshal.PtrToStructure<RegSetX64>(ptr);
                CopyContext(ctx, ref r);
                Marshal.Copy(ptrContext, context, 0, sizeof(AMD64Context));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
                Marshal.FreeHGlobal(ptrContext);
            }
            return true;
        }

        internal IEnumerable<string> GetModulesFullPath()
        {
            return _memoryMapEntries.Where(e => !string.IsNullOrEmpty(e.FilePath)).Select(e => e.FilePath).Distinct();
        }

        private unsafe void CopyContext(AMD64Context* ctx, ref RegSetX64 registerSet)
        {
            ctx->R15 = registerSet.R15;
            ctx->R14 = registerSet.R14;
            ctx->R13 = registerSet.R13;
            ctx->R12 = registerSet.R12;
            ctx->Rbp = registerSet.Rbp;
            ctx->Rbx = registerSet.Rbx;
            ctx->R11 = registerSet.R11;
            ctx->R10 = registerSet.R10;
            ctx->R9 = registerSet.R9;
            ctx->R8 = registerSet.R8;
            ctx->Rax = registerSet.Rax;
            ctx->Rcx = registerSet.Rcx;
            ctx->Rdx = registerSet.Rdx;
            ctx->Rsi = registerSet.Rsi;
            ctx->Rdi = registerSet.Rdi;
            ctx->Rip = registerSet.Rip;
            ctx->Rsp = registerSet.Rsp;
        }

        private void LoadThreads()
        {
            if (_threadIDs.Count == 0)
            {
                string taskDirPath = $"/proc/{this.ProcessId}/task";
                foreach (var taskDir in Directory.GetDirectories(taskDirPath))
                {
                    string dirName = Path.GetFileName(taskDir);
                    uint taskId;
                    if (uint.TryParse(dirName, out taskId) && taskId > 0)
                    {
                        _threadIDs.Add(taskId);
                    }
                }
            }
        }

        private void OpenMemFile()
        {
            if (_initializedMemFile)
            {
                return;
            }
            if (File.Exists("/proc/self/mem"))
            {
                _memoryStream = File.OpenRead($"/proc/{this.ProcessId}/mem");
            }
            else
            {
                // WSL 1 doesn't have /proc/<pid>/mem
            }
            _initializedMemFile = true;
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
                if (regionSize >= (ulong) bytesRequested)
                {
                    readableBytesCount += bytesRequested;
                    bytesRequested = 0;
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
            string mapsFilePath = $"/proc/{this.ProcessId}/maps";
            using (FileStream fs = File.OpenRead(mapsFilePath))
            using (StreamReader sr = new StreamReader(fs))
            {
                while (true)
                {
                    string line = sr.ReadLine();
                    if (string.IsNullOrEmpty(line))
                    {
                        break;
                    }
                    string address, permission, offset, dev, inode, path;
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
                    address = parts[0]; permission = parts[1]; offset = parts[2]; dev = parts[3]; inode = parts[4];
                    string[] addressBeginEnd = address.Split('-');
                    MemoryMapEntry entry = new MemoryMapEntry()
                    {
                        BeginAddr = Convert.ToUInt64(addressBeginEnd[0], 16),
                        EndAddr = Convert.ToUInt64(addressBeginEnd[1], 16),
                        FilePath = path,
                        FileName = string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path),
                        Permission = ParsePermission(permission)
                    };
                    result.Add(entry);
                }
            }
            return result;
        }

        private int ParsePermission(string permission)
        {
            // parse something like rwxp or r-xp. more info see 
            // https://stackoverflow.com/questions/1401359/understanding-linux-proc-id-maps
            if (permission.Length != 4)
            {
                return 0;
            }
            int r = permission[0] != '-' ? 8 : 0;   // 8: can read
            int w = permission[1] != '-' ? 4 : 0;   // 4: can write
            int x = permission[2] != '-' ? 2 : 0;   // 2: can execute
            int p = permission[3] != '-' ? 1 : 0;   // 1: private
            return r + w + x + p;
        }


        [DllImport("libc", SetLastError = true)]
        private static extern ulong ptrace(uint command, int pid, IntPtr addr, IntPtr data);

        [DllImport("libc", SetLastError = true)]
        private static extern unsafe IntPtr process_vm_readv(int pid, iovec* local_iov, UIntPtr liovcnt, iovec* remote_iov, UIntPtr riovcnt, UIntPtr flags);

        private unsafe struct iovec
        {
            public void* iov_base;
            public IntPtr iov_len;
        }

        private const uint PTRACE_GETREGS = 12;
    }

    internal class MemoryMapEntry
    {
        public ulong BeginAddr { get; set; }
        public ulong EndAddr { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public int Permission { get; set; }

        public bool IsReadable()
        {
            return (this.Permission & 8) > 0;
        }
    }
}
#endif