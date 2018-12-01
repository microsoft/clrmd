// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class HandleTableWalker
    {
        private readonly DesktopRuntimeBase _runtime;
        private readonly ClrHeap _heap;
        private int _max = 10000;
        private VISITHANDLEV2 _mV2Delegate;
        private VISITHANDLEV4 _mV4Delegate;

        public List<ClrHandle> Handles { get; }
        public byte[] V4Request
        {
            get
            {
                // MULTITHREAD ISSUE
                if (_mV4Delegate == null)
                    _mV4Delegate = VisitHandleV4;

                IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(_mV4Delegate);
                byte[] request = new byte[IntPtr.Size * 2];
                FunctionPointerToByteArray(functionPtr, request, 0);

                return request;
            }
        }

        public byte[] V2Request
        {
            get
            {
                // MULTITHREAD ISSUE
                if (_mV2Delegate == null)
                    _mV2Delegate = VisitHandleV2;

                IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(_mV2Delegate);
                byte[] request = new byte[IntPtr.Size * 2];

                FunctionPointerToByteArray(functionPtr, request, 0);

                return request;
            }
        }

        public HandleTableWalker(DesktopRuntimeBase dac)
        {
            _runtime = dac;
            _heap = dac.Heap;
            Handles = new List<ClrHandle>();
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int VISITHANDLEV4(ulong HandleAddr, ulong HandleValue, int HandleType, uint ulRefCount, ulong appDomainPtr, IntPtr token);

        private int VisitHandleV4(ulong addr, ulong obj, int hndType, uint refCnt, ulong appDomain, IntPtr unused)
        {
            Debug.Assert(unused == IntPtr.Zero);

            return AddHandle(addr, obj, hndType, refCnt, 0, appDomain);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int VISITHANDLEV2(ulong HandleAddr, ulong HandleValue, int HandleType, ulong appDomainPtr, IntPtr token);

        private int VisitHandleV2(ulong addr, ulong obj, int hndType, ulong appDomain, IntPtr unused)
        {
            Debug.Assert(unused == IntPtr.Zero);

            // V2 cannot actually get the ref count from a handle.  We'll always report the RefCount as
            // 1 in this case so the user will treat this as a strong handle (which the majority of COM
            // handles are).
            uint refCnt = 0;
            if (hndType == (uint)HandleType.RefCount)
                refCnt = 1;

            return AddHandle(addr, obj, hndType, refCnt, 0, appDomain);
        }

        public int AddHandle(ulong addr, ulong obj, int hndType, uint refCnt, uint dependentTarget, ulong appDomain)
        {
            // If we fail to get the MT of this object, just skip it and keep going
            if (!GetMethodTables(obj, out ulong mt, out ulong cmt))
                return _max-- > 0 ? 1 : 0;

            ClrHandle handle = new ClrHandle
            {
                Address = addr,
                Object = obj,
                Type = _heap.GetObjectType(obj),
                HandleType = (HandleType)hndType,
                RefCount = refCnt,
                AppDomain = _runtime.GetAppDomainByAddress(appDomain),
                DependentTarget = dependentTarget
            };

            if (dependentTarget != 0)
                handle.DependentType = _heap.GetObjectType(dependentTarget);

            Handles.Add(handle);
            handle = handle.GetInteriorHandle();
            if (handle != null)
                Handles.Add(handle);

            // Stop if we have too many handles (likely infinite loop in dac due to
            // inconsistent data).
            return _max-- > 0 ? 1 : 0;
        }

        private bool GetMethodTables(ulong obj, out ulong mt, out ulong cmt)
        {
            mt = 0;
            cmt = 0;

            byte[] data = new byte[IntPtr.Size * 3]; // TODO assumes bitness same as dump
            if (!_runtime.ReadMemory(obj, data, data.Length, out int read) || read != data.Length)
                return false;

            if (IntPtr.Size == 4)
                mt = BitConverter.ToUInt32(data, 0);
            else
                mt = BitConverter.ToUInt64(data, 0);

            if (mt == _runtime.ArrayMethodTable)
            {
                if (IntPtr.Size == 4)
                    cmt = BitConverter.ToUInt32(data, 2 * IntPtr.Size);
                else
                    cmt = BitConverter.ToUInt64(data, 2 * IntPtr.Size);
            }

            return true;
        }

        private static void FunctionPointerToByteArray(IntPtr functionPtr, byte[] request, int start)
        {
            long ptr = functionPtr.ToInt64();

            for (int i = start; i < start + sizeof(ulong); ++i)
            {
                request[i] = (byte)ptr;
                ptr >>= 8;
            }
        }
    }
}