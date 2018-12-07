// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
    internal unsafe class XPlatLiveDataTarget : DataTarget, IDataReader
    {
        private readonly uint _timeout;
        private readonly AttachFlag _flags;
        private readonly int _pid;

        private readonly List<DacLibrary> _dacLibraries = new List<DacLibrary>(2);

        public override IDataReader DataReader => this;

        public override bool IsMinidump => false;

        public override Architecture Architecture => GetArchitecture();

        private Lazy<IList<ClrInfo>> _clrVersionsLazy;

        public XPlatLiveDataTarget(int pid, uint timeout, AttachFlag flags)
        {
            _pid = pid;
            _timeout = timeout;
            _flags = flags;

            _clrVersionsLazy = new Lazy<IList<ClrInfo>>(InitClrVersions);

            // TODO: check dac arch
        }

        public override IList<ClrInfo> ClrVersions => _clrVersionsLazy.Value;

        public override uint PointerSize => unchecked((uint)IntPtr.Size);
        //Architecture == Architecture.Amd64 ? 8u
        //: Architecture == Architecture.X86 ? 4u
        //: throw new NotImplementedException();

        private IList<ClrInfo> InitClrVersions()
        {
            var versions = new List<ClrInfo>();
            //Console.Error.WriteLine($"Checking modules in {_pid}");
            foreach (ModuleInfo module in EnumerateModules())
            {
                string clrName = Path.GetFileNameWithoutExtension(module.FileName);
                //Console.Error.WriteLine($"Checking {clrName}");
                string clrNameLower = clrName?.ToLower();

                if (clrNameLower == null
                    || clrNameLower != "clr"
                    && clrNameLower != "mscorwks"
                    && clrNameLower != "coreclr"
                    && clrNameLower != "mrt100_app"
                    && clrNameLower != "libcoreclr")
                    continue;

                ClrFlavor flavor;
                switch (clrNameLower)
                {
                    case "mrt100_app":
                        //_native = module;
                        throw new NotImplementedException("mrt100_app");
                    //continue;

                    case "libcoreclr":
                    case "coreclr":
                        flavor = ClrFlavor.Core;
                        break;

                    default:
                        flavor = ClrFlavor.Desktop;
                        break;
                }

                string directoryName = Path.GetDirectoryName(module.FileName);
                string dacLocation = Path.Combine(directoryName, DacInfo.GetDacFileName(flavor, Architecture));

                bool isLinux = clrName == "libcoreclr";

                if (isLinux)
                {
                    dacLocation = Path.ChangeExtension(dacLocation, ".so");
                    if (!File.Exists(dacLocation))
                        dacLocation = Path.GetFileName(dacLocation);
                    //Console.Error.WriteLine($"Adding {dacLocation}");
                }
                else if (!File.Exists(dacLocation) || !PlatformFunctions.IsEqualFileVersion(dacLocation, module.Version))
                {
                    dacLocation = null;
                }

                VersionInfo version = module.Version;
                string dacAgnosticName = DacInfo.GetDacRequestFileName(flavor, Architecture, Architecture, version);
                string dacFileName = DacInfo.GetDacRequestFileName(flavor, IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64, Architecture, version);

                var dacInfo = new DacInfo(this, dacAgnosticName, Architecture)
                {
                    FileSize = module.FileSize,
                    TimeStamp = module.TimeStamp,
                    FileName = dacFileName,
                    Version = module.Version
                };

                versions.Add(new ClrInfo(this, flavor, module, dacInfo, dacLocation));
                //Console.Error.WriteLine($"Added {dacInfo.FileName} {dacInfo.FileSize}");
            }

            //if (versions.Count < 1)
            //{
            //    Console.Error.WriteLine("Crap! Couldn't find DAC module!");
            //    foreach (var mod in EnumerateModules())
            //        Console.Error.WriteLine($"0x{mod.ImageBase:x16} {mod.FileSize,11}b {mod.FileName}");
            //}

            ClrInfo[] result = versions.ToArray();
            Array.Sort(result);
            return result;
        }

        public override bool ReadProcessMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead) =>
            ReadMemory(address, buffer, bytesRequested, out bytesRead);

        public override object DebuggerInterface { get; } = null;

        public void Close()
        {
            Dispose();
        }

        public void Flush()
        {
        }

        public Architecture GetArchitecture() =>
            IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64;

        public uint GetPointerSize() =>
            (uint)IntPtr.Size;

        public override IEnumerable<ModuleInfo> EnumerateModules() =>
            ((IDataReader)this).EnumerateModules();

        IList<ModuleInfo> IDataReader.EnumerateModules()
        {
            using (Process proc = Process.GetProcessById(_pid))
            {
                return proc.Modules
                    .Cast<ProcessModule>()
                    .Select(
                        module => new ModuleInfo(this)
                        {
                            ImageBase = unchecked((ulong)module.BaseAddress.ToInt64()),
                            FileName = module.FileName,
                            FileSize = unchecked((uint)module.ModuleMemorySize),
                            TimeStamp = 0 // really?
                        })
                    .ToArray();
            }
        }

        public override void Dispose()
        {
            foreach (DacLibrary lib in _dacLibraries)
                lib.Dispose();
            _dacLibraries.Clear();
            GC.SuppressFinalize(this);
        }

        internal override void AddDacLibrary(DacLibrary dacLibrary)
        {
            _dacLibraries.Add(dacLibrary);
        }

        public void GetVersionInfo(ulong addr, out VersionInfo version)
        {
            string filename;
            if (IsWindows)
            {
                var sbFilename = new StringBuilder(1024);
                using (Process p = Process.GetProcessById(_pid))
                    LiveDataReader.GetModuleFileNameExA(p.Handle, new IntPtr((long)addr), sbFilename, sbFilename.Capacity);
                filename = sbFilename.ToString();
            }
            else
                filename = GetModuleFileNameXPlat(addr);

            version = PlatformFunctions.GetFileVersion(filename, out int major, out int minor, out int revision, out int patch)
                ? new VersionInfo(major, minor, revision, patch)
                : new VersionInfo();
        }

        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            fixed (byte* pByte = &buffer[0])
            {
                return ReadMemory(address, (IntPtr)pByte, bytesRequested, out bytesRead);
            }
        }

        public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            //Console.Error.WriteLine($"Attempting to read {bytesRequested} bytes from 0x{address:X8} into 0x{(ulong)buffer.ToInt64():X8}.");
            if (IsWindows)
            {
                try
                {
                    using (Process p = Process.GetProcessById(_pid))
                    {
                        return LiveDataReader.ReadProcessMemory(p.Handle, new IntPtr((long)address), buffer, bytesRequested, out bytesRead) != 0;
                    }
                }
                catch
                {
                    bytesRead = 0;
                    //Console.Error.WriteLine("Windows ReadMemory failed!");
                    return false;
                }
            }

            ulong requested = unchecked((ulong)bytesRequested);
            ulong offset = 0;
            do
            {
                ulong len = Math.Min(requested - offset, 1024);

                var local = new[]
                {
                    new iovec
                    {
                        iov_base = (IntPtr)(unchecked((ulong)buffer.ToInt64()) + offset),
                        iov_len = (UIntPtr)len
                    }
                };

                var remote = new[]
                {
                    new iovec
                    {
                        iov_base = (IntPtr)(address + offset),
                        iov_len = (UIntPtr)len
                    }
                };

                long read = LinuxFunctions.ProcessVmReadV(
                        _pid,
                        local,
                        1,
                        remote,
                        1
                    )
                    .ToInt64();

                if (read < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    //Console.Error.WriteLine($"Non-Win ReadMemory failed! Error Number: {errno:X}");
                    bytesRead = (int)offset;
                    return false;
                }

                var unsignedRead = unchecked((ulong)read);

                offset += unsignedRead;

                if (unsignedRead == len)
                    continue;

                // incomplete read, assume error? do we return false?
                bytesRead = (int)offset;
                //Console.Error.WriteLine($"Non-Win ReadMemory failed! Read {unsignedRead} in last stride, total {bytesRead} versus {requested}.");
                return false;
            } while (offset < requested);

            bytesRead = (int)offset;

            return true;
        }

        public TValue Read<TValue>(ulong addr)
        {
            var buf = new byte[Unsafe.SizeOf<TValue>()];

            if (!ReadMemory(addr, buf, buf.Length, out int bytesRead))
            {
                //Console.Error.WriteLine("Read Failed!");
                throw new NotImplementedException("Incomplete read");
            }

            if (bytesRead != buf.Length)
            {
                //Console.Error.WriteLine("Read Wrong Length!");
                throw new NotImplementedException("Incomplete read");
            }

            return Unsafe.ReadUnaligned<TValue>(ref buf[0]);
        }

        public ulong ReadPointerUnsafe(ulong addr)
        {
            return Read<UIntPtr>(addr).ToUInt64();
        }

        public uint ReadDwordUnsafe(ulong addr)
        {
            return Read<uint>(addr);
        }

        public ulong GetThreadTeb(uint thread)
        {
            //Console.Error.WriteLine("GetThreadTeb!");
            throw new NotImplementedException();
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            using (Process p = Process.GetProcessById(_pid))
            {
                foreach (ProcessThread thread in p.Threads)
                    yield return (uint)thread.Id;
            }
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            if (IsWindows)
            {
                vq = new VirtualQueryData();

                var mem = new MEMORY_BASIC_INFORMATION();
                var ptr = new IntPtr((long)addr);

                using (Process p = Process.GetProcessById(_pid))
                {
                    if (LiveDataReader.VirtualQueryEx(p.Handle, ptr, ref mem, new IntPtr(Marshal.SizeOf(mem))) == 0)
                        return false;
                }

                vq.BaseAddress = mem.BaseAddress;
                vq.Size = mem.Size;
                return true;
            }
            
            // non-windows

            ProcMapEntry entry = ProcMap.GetEntries(_pid)
                .FirstOrDefault(e => e.InRange(addr));

            if (entry != null)
            {
                vq = new VirtualQueryData(
                    entry.Start.ToUInt64(),
                    entry.Size.ToUInt64());
                return true;
            }

            vq = default;
            return false;
        }

        private string GetModuleFileNameXPlat(ulong addr)
        {
            return ProcMap.GetEntries(_pid)
                .FirstOrDefault(entry => entry.InRange(addr))
                ?.Path;
        }

        public bool GetThreadContext(uint threadId, uint contextFlags, uint contextSize, IntPtr context)
        {
            if (IsWindows)
            {
                using (SafeWin32Handle hThread = LiveDataReader.OpenThread(ThreadAccess.THREAD_GET_CONTEXT | ThreadAccess.THREAD_QUERY_INFORMATION, false, threadId))
                {
                    return LiveDataReader.GetThreadContext(hThread.DangerousGetHandle(), context);
                }
            }

            //Console.Error.WriteLine("GetThreadContext 1!");
            // maybe implement similar to: https://github.com/dotnet/coreclr/blob/master/src/ToolBox/SOS/lldbplugin/services.cpp
            //throw new NotImplementedException();
            return false;
        }

        public bool GetThreadContext(uint threadId, uint contextFlags, uint contextSize, byte[] context)
        {
            if (IsWindows)
            {
                fixed (byte* p = &context[0])
                {
                    return GetThreadContext(threadId, contextFlags, contextSize, (IntPtr)p);
                }
            }

            //Console.Error.WriteLine("GetThreadContext 2!");
            // maybe implement similar to: https://github.com/dotnet/coreclr/blob/master/src/ToolBox/SOS/lldbplugin/services.cpp
            //throw new NotImplementedException();
            return false;
        }
    }
}