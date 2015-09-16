// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
    internal class DataTargetImpl : DataTarget
    {
        private IDataReader _dataReader;
        private IDebugClient _client;
        private ClrInfo[] _versions;
        private Architecture _architecture;
        private ModuleInfo[] _modules;

        public DataTargetImpl(IDataReader dataReader, IDebugClient client)
        {
            if (dataReader == null)
                throw new ArgumentNullException("dataReader");

            _dataReader = dataReader;
            _client = client;
            _architecture = _dataReader.GetArchitecture();
        }

        public override IDataReader DataReader
        {
            get
            {
                return _dataReader;
            }
        }

        public override bool IsMinidump
        {
            get { return _dataReader.IsMinidump; }
        }

        public override Architecture Architecture
        {
            get { return _architecture; }
        }

        public override uint PointerSize
        {
            get { return _dataReader.GetPointerSize(); }
        }

        public override IList<ClrInfo> ClrVersions
        {
            get
            {
                if (_versions != null)
                    return _versions;

                List<ClrInfo> versions = new List<ClrInfo>();
                foreach (ModuleInfo module in EnumerateModules())
                {
                    string clrName = Path.GetFileNameWithoutExtension(module.FileName).ToLower();

                    if (clrName != "clr" && clrName != "mscorwks" && clrName != "coreclr" && clrName != "mrt100_app")
                        continue;

                    string dacLocation = Path.Combine(Path.GetDirectoryName(module.FileName), "mscordacwks.dll");
                    if (!File.Exists(dacLocation) || !NativeMethods.IsEqualFileVersion(module.FileName, module.Version))
                        dacLocation = null;

                    ClrFlavor flavor;
                    switch (clrName)
                    {
                        case "mrt100_app":
                            flavor = ClrFlavor.Native;
                            break;

                        case "coreclr":
                            flavor = ClrFlavor.CoreCLR;
                            break;

                        default:
                            flavor = ClrFlavor.Desktop;
                            break;
                    }

                    VersionInfo version = module.Version;
                    string dacAgnosticName = DacInfo.GetDacRequestFileName(flavor, Architecture, Architecture, version);
                    string dacFileName = DacInfo.GetDacRequestFileName(flavor, IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64, Architecture, version);

                    DacInfo dacInfo = new DacInfo(_dataReader, dacAgnosticName, Architecture);
                    dacInfo.FileSize = module.FileSize;
                    dacInfo.TimeStamp = module.TimeStamp;
                    dacInfo.FileName = dacFileName;


                    versions.Add(new ClrInfo(this, flavor, module, dacInfo, dacLocation));
                }

                _versions = versions.ToArray();
                
                Array.Sort(_versions);
                return _versions;
            }
        }

        public override bool ReadProcessMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            return _dataReader.ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }

        public override ClrRuntime CreateRuntime(string dacFilename)
        {
            if (IntPtr.Size != (int)_dataReader.GetPointerSize())
                throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

            if (string.IsNullOrEmpty(dacFilename))
                throw new ArgumentNullException("dacFilename");

            if (!File.Exists(dacFilename))
                throw new FileNotFoundException(dacFilename);

            DacLibrary lib = new DacLibrary(this, dacFilename);

            // TODO: There has to be a better way to determine this is coreclr.
            string dacFileNoExt = Path.GetFileNameWithoutExtension(dacFilename).ToLower();
            bool isCoreClr = dacFileNoExt.Contains("mscordaccore");
            bool isNative = dacFileNoExt.Contains("mrt100dac");

            int major, minor, revision, patch;
            bool res = NativeMethods.GetFileVersion(dacFilename, out major, out minor, out revision, out patch);

            DesktopVersion ver;
            if (isCoreClr)
            {
                return new V45Runtime(this, lib);
            }
            else if (isNative)
            {
                return new Native.NativeRuntime(this, lib);
            }
            else if (major == 2)
            {
                ver = DesktopVersion.v2;
            }
            else if (major == 4 && minor == 0 && patch < 10000)
            {
                ver = DesktopVersion.v4;
            }
            else
            {
                // Assume future versions will all work on the newest runtime version.
                return new V45Runtime(this, lib);
            }

            return new LegacyRuntime(this, lib, ver, patch);
        }

        public override ClrRuntime CreateRuntime(object clrDataProcess)
        {
            DacLibrary lib = new DacLibrary(this, (IXCLRDataProcess)clrDataProcess);

            // Figure out what version we are on.
            if (clrDataProcess is ISOSDac)
            {
                return new V45Runtime(this, lib);
            }
            else
            {
                byte[] buffer = new byte[Marshal.SizeOf(typeof(V2HeapDetails))];

                int val = lib.DacInterface.Request(DacRequests.GCHEAPDETAILS_STATIC_DATA, 0, null, (uint)buffer.Length, buffer);
                if ((uint)val == (uint)0x80070057)
                    return new LegacyRuntime(this, lib, DesktopVersion.v4, 10000);
                else
                    return new LegacyRuntime(this, lib, DesktopVersion.v2, 3054);
            }
        }

        public override IDebugClient DebuggerInterface
        {
            get { return _client; }
        }

        public override IEnumerable<ModuleInfo> EnumerateModules()
        {
            if (_modules == null)
                InitModules();

            return _modules;
        }

        internal override string ResolveSymbol(ulong addr)
        {
            ModuleInfo module = FindModule(addr);
            if (module == null)
                return null;

            SymbolModule sym = SymbolLocator.LoadPdb(module);
            if (sym == null)
                return null;

            return sym.FindNameForRva((uint)(addr - module.ImageBase));
        }

        private ModuleInfo FindModule(ulong addr)
        {
            if (_modules == null)
                InitModules();

            // TODO: Make binary search.
            foreach (var module in _modules)
                if (module.ImageBase <= addr && addr < module.ImageBase + module.FileSize)
                    return module;

            return null;
        }

        private void InitModules()
        {
            if (_modules == null)
            {
                var sortedModules = new List<ModuleInfo>(_dataReader.EnumerateModules());
                sortedModules.Sort((a, b) => a.ImageBase.CompareTo(b.ImageBase));
                _modules = sortedModules.ToArray();
            }
        }
        
        public override void Dispose()
        {
            _dataReader.Close();
        }
    }


    internal class DacLibrary
    {
        #region Variables
        private IntPtr _library;
        private IDacDataTarget _dacDataTarget;
        private IXCLRDataProcess _dac;
        private ISOSDac _sos;
        #endregion

        public IXCLRDataProcess DacInterface { get { return _dac; } }

        public ISOSDac SOSInterface
        {
            get
            {
                if (_sos == null)
                    _sos = (ISOSDac)_dac;

                return _sos;
            }
        }

        public DacLibrary(DataTargetImpl dataTarget, object ix)
        {
            _dac = ix as IXCLRDataProcess;
            if (_dac == null)
                throw new ArgumentException("clrDataProcess not an instance of IXCLRDataProcess");
        }

        public DacLibrary(DataTargetImpl dataTarget, string dacDll)
        {
            if (dataTarget.ClrVersions.Count == 0)
                throw new ClrDiagnosticsException(String.Format("Process is not a CLR process!"));

            _library = NativeMethods.LoadLibrary(dacDll);
            if (_library == IntPtr.Zero)
                throw new ClrDiagnosticsException("Failed to load dac: " + dacDll);

            IntPtr addr = NativeMethods.GetProcAddress(_library, "CLRDataCreateInstance");
            _dacDataTarget = new DacDataTarget(dataTarget);

            object obj;
            NativeMethods.CreateDacInstance func = (NativeMethods.CreateDacInstance)Marshal.GetDelegateForFunctionPointer(addr, typeof(NativeMethods.CreateDacInstance));
            Guid guid = new Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");
            int res = func(ref guid, _dacDataTarget, out obj);

            if (res == 0)
                _dac = obj as IXCLRDataProcess;

            if (_dac == null)
                throw new ClrDiagnosticsException("Failure loading DAC: CreateDacInstance failed 0x" + res.ToString("x"), ClrDiagnosticsException.HR.DacError);
        }

        ~DacLibrary()
        {
            if (_library != IntPtr.Zero)
                NativeMethods.FreeLibrary(_library);
        }
    }

    internal class DacDataTarget : IDacDataTarget, IMetadataLocator
    {
        private DataTargetImpl _dataTarget;
        private IDataReader _dataReader;
        private ModuleInfo[] _modules;

        public DacDataTarget(DataTargetImpl dataTarget)
        {
            _dataTarget = dataTarget;
            _dataReader = _dataTarget.DataReader;
            _modules = dataTarget.EnumerateModules().ToArray();
            Array.Sort(_modules, delegate(ModuleInfo a, ModuleInfo b) { return a.ImageBase.CompareTo(b.ImageBase); });
        }


        public void GetMachineType(out IMAGE_FILE_MACHINE machineType)
        {
            var arch = _dataReader.GetArchitecture();

            switch (arch)
            {
                case Architecture.Amd64:
                    machineType = IMAGE_FILE_MACHINE.AMD64;
                    break;

                case Architecture.X86:
                    machineType = IMAGE_FILE_MACHINE.I386;
                    break;

                case Architecture.Arm:
                    machineType = IMAGE_FILE_MACHINE.THUMB2;
                    break;

                default:
                    machineType = IMAGE_FILE_MACHINE.UNKNOWN;
                    break;
            }
        }

        ModuleInfo GetModule(ulong address)
        {
            int min = 0, max = _modules.Length - 1;

            while (min <= max)
            {
                int i = (min + max) / 2;
                ModuleInfo curr = _modules[i];

                if (curr.ImageBase <= address && address < curr.ImageBase + curr.FileSize)
                    return curr;
                else if (curr.ImageBase < address)
                    min = i + 1;
                else
                    max = i - 1;
            }

            return null;
        }

        public void GetPointerSize(out uint pointerSize)
        {
            pointerSize = _dataReader.GetPointerSize();
        }

        public void GetImageBase(string imagePath, out ulong baseAddress)
        {
            imagePath = Path.GetFileNameWithoutExtension(imagePath);

            foreach (ModuleInfo module in _modules)
            {
                string moduleName = Path.GetFileNameWithoutExtension(module.FileName);
                if (imagePath.Equals(moduleName, StringComparison.CurrentCultureIgnoreCase))
                {
                    baseAddress = module.ImageBase;
                    return;
                }
            }

            throw new Exception();
        }

        public unsafe int ReadVirtual(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            int read = 0;
            if (_dataReader.ReadMemory(address, buffer, bytesRequested, out read))
            {
                bytesRead = read;
                return 0;
            }

            ModuleInfo info = GetModule(address);
            if (info != null)
            {
                PEFile file = _dataTarget.SymbolLocator.LoadBinary(info.FileName, info.TimeStamp, info.FileSize, true);
                if (file != null)
                {
                    PEBuffer peBuffer = file.AllocBuff();

                    int rva = checked((int)(address - info.ImageBase));

                    if (file.Header.TryGetFileOffsetFromRva(rva, out rva))
                    {
                        byte* dst = (byte*)buffer.ToPointer();
                        byte* src = peBuffer.Fetch(rva, bytesRequested);

                        for (int i = 0; i < bytesRequested; i++)
                            dst[i] = src[i];

                        bytesRead = bytesRequested;
                        return 0;
                    }

                    file.FreeBuff(peBuffer);
                }
            }

            bytesRead = 0;
            return -1;
        }

        public int ReadMemory(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
        {
            int read = 0;
            if (_dataReader.ReadMemory(address, buffer, (int)bytesRequested, out read))
            {
                bytesRead = (uint)read;
                return 0;
            }

            bytesRead = 0;
            return -1;
        }

        public int ReadVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
        {
            return ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }

        public void WriteVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesWritten)
        {
            // This gets used by MemoryBarrier() calls in the dac, which really shouldn't matter what we do here.
            bytesWritten = bytesRequested;
        }

        public void GetTLSValue(uint threadID, uint index, out ulong value)
        {
            // TODO:  Validate this is not used?
            value = 0;
        }

        public void SetTLSValue(uint threadID, uint index, ulong value)
        {
            throw new NotImplementedException();
        }

        public void GetCurrentThreadID(out uint threadID)
        {
            threadID = 0;
        }

        public void GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            _dataReader.GetThreadContext(threadID, contextFlags, contextSize, context);
        }

        public void SetThreadContext(uint threadID, uint contextSize, IntPtr context)
        {
            throw new NotImplementedException();
        }

        public void Request(uint reqCode, uint inBufferSize, IntPtr inBuffer, IntPtr outBufferSize, out IntPtr outBuffer)
        {
            throw new NotImplementedException();
        }

        public int GetMetadata(string filename, uint imageTimestamp, uint imageSize, IntPtr mvid, uint mdRva, uint flags, uint bufferSize, byte[] buffer, IntPtr dataSize)
        {
            PEFile file = _dataTarget.SymbolLocator.LoadBinary(filename, imageTimestamp, imageSize, true);
            if (file == null)
                return -1;

            var comDescriptor = file.Header.ComDescriptorDirectory;
            if (comDescriptor.VirtualAddress == 0)
                return -1;

            PEBuffer peBuffer = file.AllocBuff();
            if (mdRva == 0)
            {
                IntPtr hdr = file.SafeFetchRVA((int)comDescriptor.VirtualAddress, (int)comDescriptor.Size, peBuffer);

                IMAGE_COR20_HEADER corhdr = (IMAGE_COR20_HEADER)Marshal.PtrToStructure(hdr, typeof(IMAGE_COR20_HEADER));
                if (bufferSize < corhdr.MetaData.Size)
                {
                    file.FreeBuff(peBuffer);
                    return -1;
                }

                mdRva = corhdr.MetaData.VirtualAddress;
                bufferSize = corhdr.MetaData.Size;
            }

            IntPtr ptr = file.SafeFetchRVA((int)mdRva, (int)bufferSize, peBuffer);
            Marshal.Copy(ptr, buffer, 0, (int)bufferSize);

            file.FreeBuff(peBuffer);
            return 0;
        }
    }
}
