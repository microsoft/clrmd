using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    internal unsafe class VtableBuilder
    {
        private readonly Guid _guid;
        private readonly COMCallableIUnknown _wrapper;
        private readonly List<Delegate> _delegates = new List<Delegate>();

        public VtableBuilder(COMCallableIUnknown wrapper, Guid guid)
        {
            _guid = guid;
            _wrapper = wrapper;
        }

        public void AddMethod(Delegate func)
        {
#if DEBUG
            if (func.Method.GetParameters().First().ParameterType != typeof(IntPtr))
                throw new InvalidOperationException();

            var attrs = func.GetType().GetCustomAttributes(false);
            if (attrs.Where(c => c is UnmanagedFunctionPointerAttribute).Count() != 1)
                throw new InvalidOperationException();

            if (func.Method.ReturnType != typeof(int))
                throw new InvalidOperationException();
#endif

            _delegates.Add(func);
        }

        internal IntPtr Complete()
        {
            var obj = Marshal.AllocHGlobal(IntPtr.Size);

            var vtablePartSize = _delegates.Count * IntPtr.Size;
            var vtable = (IntPtr*)Marshal.AllocHGlobal(vtablePartSize + sizeof(IUnknownVTable));
            *(void**)obj = vtable;

            var iunk = _wrapper.IUnknown;
            *vtable++ = iunk.QueryInterface;
            *vtable++ = iunk.AddRef;
            *vtable++ = iunk.Release;

            foreach (var d in _delegates)
                *vtable++ = Marshal.GetFunctionPointerForDelegate(d);

            _wrapper.RegisterInterface(_guid, obj, _delegates);
            return obj;
        }
    }
}