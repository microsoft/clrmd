// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public sealed unsafe class ClrStackWalk : CallableCOMWrapper
    {
        private static readonly Guid IID_IXCLRDataStackWalk = new Guid("E59D8D22-ADA7-49a2-89B5-A415AFCFC95F");

        private RequestDelegate? _request;
        private NextDelegate? _next;
        private GetContextDelegate? _getContext;

        public ClrStackWalk(DacLibrary library, IntPtr pUnk)
            : base(library?.OwningLibrary, IID_IXCLRDataStackWalk, pUnk)
        {
        }

        private ref readonly IXCLRDataStackWalkVTable VTable => ref Unsafe.AsRef<IXCLRDataStackWalkVTable>(_vtable);

        public ClrDataAddress GetFrameVtable()
        {
            InitDelegate(ref _request, VTable.Request);

            long ptr = 0xcccccccc;

            int hr = _request(Self, 0xf0000000, 0, null, 8u, (byte*)&ptr);
            return hr == S_OK ? new ClrDataAddress(ptr) : default;
        }

        public bool Next()
        {
            InitDelegate(ref _next, VTable.Next);

            int hr = _next(Self);
            return hr == S_OK;
        }

        public bool GetContext(uint contextFlags, int contextBufSize, out int contextSize, byte[] buffer)
        {
            InitDelegate(ref _getContext, VTable.GetContext);

            int hr = _getContext(Self, contextFlags, contextBufSize, out contextSize, buffer);
            return hr == S_OK;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetContextDelegate(IntPtr self, uint contextFlags, int contextBufSize, out int contextSize, byte[] buffer);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int NextDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int RequestDelegate(
            IntPtr self,
            uint reqCode,
            uint inBufferSize,
            byte* inBuffer,
            uint outBufferSize,
            byte* outBuffer);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IXCLRDataStackWalkVTable
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