// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DbgEng;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    internal unsafe class DacDataTargetWrapper : COMCallableIUnknown
    {
        public const ulong MagicCallbackConstant = 0x43;

        private static readonly Guid IID_IDacDataTarget = new Guid("3E11CCEE-D08B-43e5-AF01-32717A64DA03");
        private static readonly Guid IID_IMetadataLocator = new Guid("aa8fa804-bc05-4642-b2c5-c353ed22fc63");

        private readonly DataTarget _dataTarget;
        private readonly IDataReader _dataReader;
        private readonly ModuleInfo[] _modules;

        private Action? _callback;
        private volatile int _callbackContext;

        private uint? _nextThreadId;
        private ulong? _nextTLSValue;

        public IntPtr IDacDataTarget { get; }

        public DacDataTargetWrapper(DataTarget dataTarget)
        {
            _dataTarget = dataTarget;
            _dataReader = _dataTarget.DataReader;
            _modules = dataTarget.EnumerateModules().ToArray();
            Array.Sort(_modules, (left, right) => left.ImageBase.CompareTo(right.ImageBase));

            VTableBuilder builder = AddInterface(IID_IDacDataTarget, false);
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

            builder = AddInterface(IID_IMetadataLocator, false);
            builder.AddMethod(new GetMetadataDelegate(GetMetadata));
            builder.Complete();
        }

        public void EnterMagicCallbackContext() => Interlocked.Increment(ref _callbackContext);

        public void ExitMagicCallbackContext() => Interlocked.Decrement(ref _callbackContext);

        public void SetMagicCallback(Action flushCallback) => _callback = flushCallback;

        public int GetMachineType(IntPtr self, out IMAGE_FILE_MACHINE machineType)
        {
            machineType = _dataReader.Architecture switch
            {
                Architecture.Amd64 => IMAGE_FILE_MACHINE.AMD64,
                Architecture.X86 => IMAGE_FILE_MACHINE.I386,
                Architecture.Arm => IMAGE_FILE_MACHINE.THUMB2,
                Architecture.Arm64 => IMAGE_FILE_MACHINE.ARM64,
                _ => IMAGE_FILE_MACHINE.UNKNOWN,
            };
            return S_OK;
        }

        private ModuleInfo? GetModule(ulong address)
        {
            int min = 0, max = _modules.Length - 1;

            while (min <= max)
            {
                int i = (min + max) / 2;
                ModuleInfo curr = _modules[i];

                if (curr.ImageBase <= address && address < curr.ImageBase + (ulong)curr.FileSize)
                    return curr;

                if (curr.ImageBase < address)
                    min = i + 1;
                else
                    max = i - 1;
            }

            return null;
        }

        public int GetPointerSize(IntPtr self, out int pointerSize)
        {
            pointerSize = _dataReader.PointerSize;
            return S_OK;
        }

        public int GetImageBase(IntPtr self, string imagePath, out ulong baseAddress)
        {
            imagePath = Path.GetFileNameWithoutExtension(imagePath);

            foreach (ModuleInfo module in _modules)
            {
                string? moduleName = Path.GetFileNameWithoutExtension(module.FileName);
                if (imagePath.Equals(moduleName, StringComparison.CurrentCultureIgnoreCase))
                {
                    baseAddress = module.ImageBase;
                    return S_OK;
                }
            }

            baseAddress = 0;
            return E_FAIL;
        }

        public int ReadVirtual(IntPtr _, ClrDataAddress cda, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            ulong address = cda;
            Span<byte> span = new Span<byte>(buffer.ToPointer(), bytesRequested);

            if (address == MagicCallbackConstant && _callbackContext > 0)
            {
                // See comment in RuntimeBuilder.FlushDac
                _callback?.Invoke();
                bytesRead = 0;
                return E_FAIL;
            }

            if (_dataReader.Read(address, span, out int read))
            {
                bytesRead = read;
                return S_OK;
            }

            bytesRead = 0;
            ModuleInfo? info = GetModule(address);
            if (info != null)
            {
                string? filePath = null;
                if (!string.IsNullOrEmpty(info.FileName))
                {
                    if (info.FileName!.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
                    {
                        // TODO
                        Debug.WriteLine($"TODO: Implement reading from module '{info.FileName}'");
                        return E_NOTIMPL;
                    }

                    filePath = _dataTarget.BinaryLocator.FindBinary(info.FileName!, info.TimeStamp, info.FileSize, true);
                }

                if (filePath is null)
                {
                    bytesRead = 0;
                    return E_FAIL;
                }

                // We do not put a using statement here to prevent needing to load/unload the binary over and over.
                PEImage? peimage = _dataTarget.LoadPEImage(filePath);
                if (peimage != null)
                {
                    lock (peimage)
                    {
                        DebugOnly.Assert(peimage.IsValid);
                        int rva = checked((int)(address - info.ImageBase));
                        bytesRead = peimage.Read(rva, span);
                        return S_OK;
                    }
                }
            }

            return E_FAIL;
        }

        public int WriteVirtual(IntPtr self, ClrDataAddress address, IntPtr buffer, uint bytesRequested, out uint bytesWritten)
        {
            // This gets used by MemoryBarrier() calls in the dac, which really shouldn't matter what we do here.
            bytesWritten = bytesRequested;
            return S_OK;
        }

        public void SetNextCurrentThreadId(uint? threadId)
        {
            _nextThreadId = threadId;
        }

        internal void SetNextTLSValue(ulong? value)
        {
            _nextTLSValue = value;
        }

        public int GetTLSValue(IntPtr self, uint threadID, uint index, out ulong value)
        {
            if (_nextTLSValue is ulong nextTLSValue)
            {
                value = nextTLSValue;
                return S_OK;
            }

            value = 0;
            return E_FAIL;
        }

        public int SetTLSValue(IntPtr self, uint threadID, uint index, ClrDataAddress value)
        {
            return E_FAIL;
        }

        public int GetCurrentThreadID(IntPtr self, out uint threadID)
        {
            if (_nextThreadId is uint nextThreadId)
            {
                threadID = nextThreadId;
                return S_OK;
            }

            threadID = 0;
            return E_FAIL;
        }

        public int GetThreadContext(IntPtr self, uint threadID, uint contextFlags, int contextSize, IntPtr context)
        {
            Span<byte> span = new Span<byte>(context.ToPointer(), contextSize);
            if (_dataReader.GetThreadContext(threadID, contextFlags, span))
                return S_OK;

            return E_FAIL;
        }

        public int Request(IntPtr self, uint reqCode, uint inBufferSize, IntPtr inBuffer, IntPtr outBufferSize, out IntPtr outBuffer)
        {
            outBuffer = IntPtr.Zero;
            return E_NOTIMPL;
        }

        public unsafe int GetMetadata(
            IntPtr self,
            string fileName,
            int imageTimestamp,
            int imageSize,
            IntPtr mvid,
            uint mdRva,
            uint flags,
            uint bufferSize,
            IntPtr buffer,
            int* pDataSize)
        {
            if (buffer == IntPtr.Zero)
                return E_INVALIDARG;

            string? filePath = _dataTarget.BinaryLocator.FindBinary(fileName, imageTimestamp, imageSize, true);
            if (filePath is null)
                return E_FAIL;

            // We do not put a using statement here to prevent needing to load/unload the binary over and over.
            PEImage? peimage = _dataTarget.LoadPEImage(filePath);
            if (peimage is null)
                return E_FAIL;

            lock (peimage)
            {
                CorHeader? corHeader = peimage.CorHeader;
                if (corHeader is null)
                    return E_FAIL;

                DebugOnly.Assert(peimage.IsValid);

                uint rva = mdRva;
                uint size = bufferSize;
                if (rva == 0)
                {
                    IMAGE_DATA_DIRECTORY metadata = corHeader.Metadata;
                    if (metadata.VirtualAddress == 0)
                        return E_FAIL;

                    rva = metadata.VirtualAddress;
                    size = Math.Min(bufferSize, metadata.Size);
                }

                checked
                {
                    int read = peimage.Read((int)rva, new Span<byte>(buffer.ToPointer(), (int)size));
                    if (pDataSize != null)
                        *pDataSize = read;
                }
            }

            return S_OK;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetMetadataDelegate(
            IntPtr self,
            [In][MarshalAs(UnmanagedType.LPWStr)] string fileName,
            int imageTimestamp,
            int imageSize,
            IntPtr mvid,
            uint mdRva,
            uint flags,
            uint bufferSize,
            IntPtr buffer,
            int* dataSize);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetMachineTypeDelegate(IntPtr self, out IMAGE_FILE_MACHINE machineType);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetPointerSizeDelegate(IntPtr self, out int pointerSize);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetImageBaseDelegate(IntPtr self, [In][MarshalAs(UnmanagedType.LPWStr)] string imagePath, out ulong baseAddress);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int ReadVirtualDelegate(
            IntPtr self,
            ClrDataAddress address,
            IntPtr buffer,
            int bytesRequested,
            out int bytesRead);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int WriteVirtualDelegate(
            IntPtr self,
            ClrDataAddress address,
            IntPtr buffer,
            uint bytesRequested,
            out uint bytesWritten);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetTLSValueDelegate(
            IntPtr self,
            uint threadID,
            uint index,
            out ulong value);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int SetTLSValueDelegate(
            IntPtr self,
            uint threadID,
            uint index,
            ClrDataAddress value);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetCurrentThreadIDDelegate(IntPtr self, out uint threadID);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetThreadContextDelegate(
            IntPtr self,
            uint threadID,
            uint contextFlags,
            int contextSize,
            IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int SetThreadContextDelegate(
            IntPtr self,
            uint threadID,
            uint contextSize,
            IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int RequestDelegate(
            IntPtr self,
            uint reqCode,
            uint inBufferSize,
            IntPtr inBuffer,
            IntPtr outBufferSize,
            out IntPtr outBuffer);
    }
}