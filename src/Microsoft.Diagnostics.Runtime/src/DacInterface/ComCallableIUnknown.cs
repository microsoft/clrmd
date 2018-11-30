// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    internal unsafe class COMCallableIUnknown : COMHelper
    {
        private readonly GCHandle _handle;
        private int _refCount;

        private readonly Dictionary<Guid, IntPtr> _interfaces = new Dictionary<Guid, IntPtr>();
        private readonly List<Delegate> _delegates = new List<Delegate>();

        public IntPtr IUnknownObject { get; }
        public IUnknownVTable IUnknown => **(IUnknownVTable**)IUnknownObject;

        public COMCallableIUnknown()
        {
            _handle = GCHandle.Alloc(this);

            var vtable = (IUnknownVTable*)Marshal.AllocHGlobal(sizeof(IUnknownVTable)).ToPointer();
            QueryInterfaceDelegate qi = QueryInterfaceImpl;
            vtable->QueryInterface = Marshal.GetFunctionPointerForDelegate(qi);
            _delegates.Add(qi);

            var addRef = new AddRefDelegate(AddRefImpl);
            vtable->AddRef = Marshal.GetFunctionPointerForDelegate(addRef);
            _delegates.Add(addRef);

            var release = new ReleaseDelegate(ReleaseImpl);
            vtable->Release = Marshal.GetFunctionPointerForDelegate(release);
            _delegates.Add(release);

            IUnknownObject = Marshal.AllocHGlobal(IntPtr.Size);
            *(void**)IUnknownObject = vtable;

            _interfaces.Add(IUnknownGuid, IUnknownObject);
        }

        public VtableBuilder AddInterface(Guid guid)
        {
            return new VtableBuilder(this, guid);
        }

        internal void RegisterInterface(Guid guid, IntPtr clsPtr, List<Delegate> keepAlive)
        {
            _interfaces.Add(guid, clsPtr);
            _delegates.AddRange(keepAlive);
        }

        private int QueryInterfaceImpl(IntPtr self, ref Guid guid, out IntPtr ptr)
        {
            if (_interfaces.TryGetValue(guid, out var value))
            {
                Interlocked.Increment(ref _refCount);
                ptr = value;
                return S_OK;
            }

            ptr = IntPtr.Zero;
            return E_NOINTERFACE;
        }

        private int ReleaseImpl(IntPtr self)
        {
            var count = Interlocked.Decrement(ref _refCount);
            if (count <= 0)
            {
                foreach (var ptr in _interfaces.Values)
                {
                    var val = (IntPtr*)ptr;
                    Marshal.FreeHGlobal(*val);
                    Marshal.FreeHGlobal(ptr);
                }

                _handle.Free();
            }

            return count;
        }

        private int AddRefImpl(IntPtr self)
        {
            return Interlocked.Increment(ref _refCount);
        }
    }
}