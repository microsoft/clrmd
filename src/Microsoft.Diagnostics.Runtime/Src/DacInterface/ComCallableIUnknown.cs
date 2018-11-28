using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    unsafe class COMCallableIUnknown : COMHelper
    {
        private readonly GCHandle _handle;
        private int _refCount = 0;

        private readonly Dictionary<Guid, IntPtr> _interfaces = new Dictionary<Guid, IntPtr>();
        private readonly List<Delegate> _delegates = new List<Delegate>();

        public IntPtr IUnknownObject { get; }

        public IUnknownVTable IUnknown => **(IUnknownVTable**)IUnknownObject;


        public COMCallableIUnknown()
        {
            _handle = GCHandle.Alloc(this);

            IUnknownVTable* vtable = (IUnknownVTable*)Marshal.AllocHGlobal(sizeof(IUnknownVTable)).ToPointer();
            QueryInterfaceDelegate qi = new QueryInterfaceDelegate(QueryInterfaceImpl);
            vtable->QueryInterface = Marshal.GetFunctionPointerForDelegate(qi);
            _delegates.Add(qi);

            AddRefDelegate addRef = new AddRefDelegate(AddRefImpl);
            vtable->AddRef = Marshal.GetFunctionPointerForDelegate(addRef);
            _delegates.Add(addRef);


            ReleaseDelegate release = new ReleaseDelegate(ReleaseImpl);
            vtable->Release = Marshal.GetFunctionPointerForDelegate(release);
            _delegates.Add(release);


            IUnknownObject = Marshal.AllocHGlobal(IntPtr.Size);
            *(void**)IUnknownObject = vtable;

            _interfaces.Add(IUnknownGuid, IUnknownObject);
        }
        public VtableBuilder AddInterface(Guid guid) => new VtableBuilder(this, guid);

        internal void RegisterInterface(Guid guid, IntPtr clsPtr, List<Delegate> keepAlive)
        {
            _interfaces.Add(guid, clsPtr);
            _delegates.AddRange(keepAlive);
        }

        private int QueryInterfaceImpl(IntPtr self, ref Guid guid, out IntPtr ptr)
        {
            if (_interfaces.TryGetValue(guid, out IntPtr value))
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
            int count = Interlocked.Decrement(ref _refCount);
            if (count <= 0)
            {
                foreach (IntPtr ptr in _interfaces.Values)
                {
                    IntPtr* val = (IntPtr*)ptr;
                    Marshal.FreeHGlobal(*val);
                    Marshal.FreeHGlobal(ptr);
                }

                _handle.Free();
            }

            return count;
        }

        private int AddRefImpl(IntPtr self) => Interlocked.Increment(ref _refCount);
    }
}
