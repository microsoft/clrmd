// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    internal unsafe class VTableBuilder
    {
        private readonly Guid _guid;
        private readonly ComCallableIUnknown _wrapper;
        private readonly List<Delegate> _delegates = new List<Delegate>();

        public VTableBuilder(ComCallableIUnknown wrapper, Guid guid)
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
            if (attrs.Count(c => c is UnmanagedFunctionPointerAttribute) != 1)
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