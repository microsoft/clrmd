using Microsoft.Diagnostics.Runtime.ICorDebug;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.ComWrappers
{
    unsafe class DacDataTargetWrapper : COMCallableIUnknown, ICorDebugDataTarget
    {
        private static readonly Guid IID_IDacDataTarget = new Guid("3E11CCEE-D08B-43e5-AF01-32717A64DA03");
        private static readonly Guid IID_IMetadataLocator = new Guid("aa8fa804-bc05-4642-b2c5-c353ed22fc63");

        private readonly DataTargetImpl _dataTarget;
        private readonly IDataReader _dataReader;
        private readonly ModuleInfo[] _modules;

        public IntPtr IDacDataTarget { get; }

        public DacDataTargetWrapper(DataTargetImpl dataTarget)
        {
            _dataTarget = dataTarget;
            _dataReader = _dataTarget.DataReader;
            _modules = dataTarget.EnumerateModules().ToArray();
            Array.Sort(_modules, delegate (ModuleInfo a, ModuleInfo b) { return a.ImageBase.CompareTo(b.ImageBase); });

            VtableBuilder builder = AddInterface(IID_IDacDataTarget);
            builder.AddMethod(new GetMachineTypeDelegate(GetMachineType));
            builder.AddMethod(new GetPointerSizeDelegate(GetPointerSize));
            builder.AddMethod(new GetImageBaseDelegate(GetImageBase));
            builder.AddMethod(new ReadVirtualDelegate(ReadVirtual));
            builder.AddMethod(new WriteVirtualDelegate(WriteVirtual));
            builder.AddMethod(new GetTLSValueDelegate(GetTLSValue));
            builder.AddMethod(new SetTLSValueDelegate(SetTLSValue));
            builder.AddMethod(new GetCurrentThreadIDDelegate(GetCurrentThreadID));
            builder.AddMethod(new GetThreadContextDelegate(GetThreadContext));
            builder.AddMethod(new RequestDelegate(Request));
            IDacDataTarget = builder.Complete();

            builder = AddInterface(IID_IMetadataLocator);
            builder.AddMethod(new GetMetadataDelegate(GetMetadata));
            builder.Complete();
        }
        
        public int ReadVirtual(IntPtr self, ulong address, IntPtr buffer, uint bytesRequested, out uint bytesRead)
        {
            if (ReadVirtual(self, address, buffer, (int)bytesRequested, out int read) >= 0)
            {
                bytesRead = (uint)read;
                return S_OK;
            }

            bytesRead = 0;
            return E_FAIL;
        }

        public int GetMachineType(IntPtr self, out IMAGE_FILE_MACHINE machineType)
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

            return S_OK;
        }

        private ModuleInfo GetModule(ulong address)
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

        public int GetPointerSize(IntPtr self, out uint pointerSize)
        {
            pointerSize = _dataReader.GetPointerSize();
            return S_OK;
        }

        public int GetImageBase(IntPtr self, string imagePath, out ulong baseAddress)
        {
            imagePath = Path.GetFileNameWithoutExtension(imagePath);

            foreach (ModuleInfo module in _modules)
            {
                string moduleName = Path.GetFileNameWithoutExtension(module.FileName);
                if (imagePath.Equals(moduleName, StringComparison.CurrentCultureIgnoreCase))
                {
                    baseAddress = module.ImageBase;
                    return S_OK;
                }
            }

            baseAddress = 0;
            return E_FAIL;
        }

        public unsafe int ReadVirtual(IntPtr self, ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            if (_dataReader.ReadMemory(address, buffer, bytesRequested, out int read))
            {
                bytesRead = read;
                return S_OK;
            }

            bytesRead = 0;
            ModuleInfo info = GetModule(address);
            if (info != null)
            {
                if (Path.GetExtension(info.FileName).ToLower() == ".so")
                {
                    // TODO
                    System.Diagnostics.Debug.WriteLine($"TODO: Implement reading from module '{info.FileName}'");
                    return E_NOTIMPL;
                }

                string filePath = _dataTarget.SymbolLocator.FindBinary(info.FileName, info.TimeStamp, info.FileSize, true);
                if (filePath == null)
                {
                    bytesRead = 0;
                    return E_FAIL;
                }

                // We do not put a using statement here to prevent needing to load/unload the binary over and over.
                PEFile file = _dataTarget.FileLoader.LoadPEFile(filePath);
                if (file?.Header != null)
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
                        return S_OK;
                    }

                    file.FreeBuff(peBuffer);
                }
            }
            
            return E_FAIL;
        }

        public int ReadMemory(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
        {
            if (_dataReader.ReadMemory(address, buffer, (int)bytesRequested, out int read))
            {
                bytesRead = (uint)read;
                return S_OK;
            }

            bytesRead = 0;
            return E_FAIL;
        }

        public int ReadVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
        {
            return ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }

        public int WriteVirtual(IntPtr self, ulong address, IntPtr buffer, uint bytesRequested, out uint bytesWritten)
        {
            // This gets used by MemoryBarrier() calls in the dac, which really shouldn't matter what we do here.
            bytesWritten = bytesRequested;
            return S_OK;
        }

        public int GetTLSValue(IntPtr self, uint threadID, uint index, out ulong value)
        {
            value = 0;
            return E_FAIL;
        }

        public int SetTLSValue(IntPtr self, uint threadID, uint index, ulong value)
        {
            return E_FAIL;
        }

        public int GetCurrentThreadID(IntPtr self, out uint threadID)
        {
            threadID = 0;
            return E_FAIL;
        }

        public int GetThreadContext(IntPtr self, uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            if (_dataReader.GetThreadContext(threadID, contextFlags, contextSize, context))
                return S_OK;

            return E_FAIL;
        }

        public int SetThreadContext(IntPtr self, uint threadID, uint contextSize, IntPtr context)
        {
            return E_NOTIMPL;
        }

        public int Request(IntPtr self, uint reqCode, uint inBufferSize, IntPtr inBuffer, IntPtr outBufferSize, out IntPtr outBuffer)
        {
            outBuffer = IntPtr.Zero;
            return E_NOTIMPL;
        }

        
        public int GetMetadata(IntPtr self, string filename, uint imageTimestamp, uint imageSize, IntPtr mvid, uint mdRva, uint flags, uint bufferSize, byte[] buffer, IntPtr dataSize)
        {
            string filePath = _dataTarget.SymbolLocator.FindBinary(filename, imageTimestamp, imageSize, true);
            if (filePath == null)
                return E_FAIL;

            // We do not put a using statement here to prevent needing to load/unload the binary over and over.
            PEFile file = _dataTarget.FileLoader.LoadPEFile(filePath);
            if (file == null)
                return E_FAIL;

            var comDescriptor = file.Header.ComDescriptorDirectory;
            if (comDescriptor.VirtualAddress == 0)
                return E_FAIL;

            PEBuffer peBuffer = file.AllocBuff();
            if (mdRva == 0)
            {
                IntPtr hdr = file.SafeFetchRVA((int)comDescriptor.VirtualAddress, (int)comDescriptor.Size, peBuffer);

                IMAGE_COR20_HEADER corhdr = (IMAGE_COR20_HEADER)Marshal.PtrToStructure(hdr, typeof(IMAGE_COR20_HEADER));
                if (bufferSize < corhdr.MetaData.Size)
                {
                    file.FreeBuff(peBuffer);
                    return E_FAIL;
                }

                mdRva = corhdr.MetaData.VirtualAddress;
                bufferSize = corhdr.MetaData.Size;
            }

            IntPtr ptr = file.SafeFetchRVA((int)mdRva, (int)bufferSize, peBuffer);
            Marshal.Copy(ptr, buffer, 0, (int)bufferSize);

            file.FreeBuff(peBuffer);
            return S_OK;
        }


        CorDebugPlatform ICorDebugDataTarget.GetPlatform()
        {
            var arch = _dataReader.GetArchitecture();

            switch (arch)
            {
                case Architecture.Amd64:
                    return CorDebugPlatform.CORDB_PLATFORM_WINDOWS_AMD64;

                case Architecture.X86:
                    return CorDebugPlatform.CORDB_PLATFORM_WINDOWS_X86;

                case Architecture.Arm:
                    return CorDebugPlatform.CORDB_PLATFORM_WINDOWS_ARM;

                default:
                    throw new Exception();
            }
        }

        uint ICorDebugDataTarget.ReadVirtual(ulong address, IntPtr buffer, uint bytesRequested)
        {
            if (ReadVirtual(IntPtr.Zero, address, buffer, (int)bytesRequested, out int read) >= 0)
                return (uint)read;

            throw new Exception();
        }

        void ICorDebugDataTarget.GetThreadContext(uint threadId, uint contextFlags, uint contextSize, IntPtr context)
        {
            if (!_dataReader.GetThreadContext(threadId, contextFlags, contextSize, context))
                throw new Exception();
        }

        #region Delegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetMetadataDelegate(IntPtr self, string filename, uint imageTimestamp, uint imageSize, IntPtr mvid, uint mdRva, uint flags, uint bufferSize, byte[] buffer, IntPtr dataSize);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetMachineTypeDelegate(IntPtr self, out IMAGE_FILE_MACHINE machineType);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetPointerSizeDelegate(IntPtr self, out uint pointerSize);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetImageBaseDelegate(IntPtr self, [In, MarshalAs(UnmanagedType.LPWStr)] string imagePath, out ulong baseAddress);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ReadVirtualDelegate(IntPtr self, ulong address,
                        IntPtr buffer,
                        int bytesRequested,
                        out int bytesRead);


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int WriteVirtualDelegate(IntPtr self, ulong address,
                         IntPtr buffer,
                         uint bytesRequested,
                         out uint bytesWritten);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetTLSValueDelegate(IntPtr self, uint threadID,
                            uint index,
                            out ulong value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int SetTLSValueDelegate(IntPtr self, uint threadID,
                        uint index,
                        ulong value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetCurrentThreadIDDelegate(IntPtr self, out uint threadID);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetThreadContextDelegate(IntPtr self, uint threadID,
                             uint contextFlags,
                             uint contextSize,
                             IntPtr context);


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int SetThreadContextDelegate(IntPtr self, uint threadID,
                              uint contextSize,
                              IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int RequestDelegate(IntPtr self, uint reqCode,
                    uint inBufferSize,
                    IntPtr inBuffer,
                    IntPtr outBufferSize,
                    out IntPtr outBuffer);

        #endregion
    }
}
