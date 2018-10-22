using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public unsafe sealed class ClrStackWalk : CallableCOMWrapper
    {
        private static Guid IID_IXCLRDataStackWalk = new Guid("E59D8D22-ADA7-49a2-89B5-A415AFCFC95F");

        private IXCLRDataStackWalkVTable* VTable => (IXCLRDataStackWalkVTable*)_vtable;

        private readonly byte[] _ulongBuffer = new byte[8];
        private RequestDelegate _request;
        private NextDelegate _next;
        private GetContextDelegate _getContext;

        public ClrStackWalk(DacLibrary library, IntPtr pUnk)
            : base(library, ref IID_IXCLRDataStackWalk, pUnk)
        {
        }

        public ulong GetFrameVtable()
        {
            InitDelegate(ref _request, VTable->Request);
            
            int hr = _request(Self, 0xf0000000, 0, null, (uint)_ulongBuffer.Length, _ulongBuffer);
            if (hr == S_OK)
                return BitConverter.ToUInt64(_ulongBuffer, 0);

            return 0;
        }

        public bool Next()
        {
            InitDelegate(ref _next, VTable->Next);

            int hr = _next(Self);
            return hr == S_OK;
        }

        public bool GetContext(uint contextFlags, uint contextBufSize, out uint contextSize, byte[] buffer)
        {
            InitDelegate(ref _getContext, VTable->GetContext);

            int hr = _getContext(Self, contextFlags, contextBufSize, out contextSize, buffer);
            return hr == S_OK;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetContextDelegate(IntPtr self, uint contextFlags, uint contextBufSize, out uint contextSize, byte[] buffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int NextDelegate(IntPtr self);


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int RequestDelegate(IntPtr self, uint reqCode, uint inBufferSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] inBuffer,
                    uint outBufferSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] outBuffer);
    }


#pragma warning disable CS0169
#pragma warning disable CS0649
    internal struct IXCLRDataStackWalkVTable
    {
        public readonly IntPtr GetContext;
        private readonly IntPtr GetContext2;
        public readonly IntPtr Next;
        private readonly IntPtr GetStackSizeSkipped;
        private readonly IntPtr GetFrameType;
        public readonly IntPtr GetFrame;
        public readonly IntPtr Request;
        private readonly IntPtr SetContext2;
    }
}