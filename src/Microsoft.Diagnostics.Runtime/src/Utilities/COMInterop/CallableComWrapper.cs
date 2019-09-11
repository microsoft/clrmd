// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    public unsafe class CallableCOMWrapper : COMHelper, IDisposable
    {
        private bool _disposed;

        protected IntPtr Self { get; }
        private readonly IUnknownVTable* _unknownVTable;
        private readonly RefCountedFreeLibrary _library;

        protected void* _vtable => _unknownVTable + 1;

        private ReleaseDelegate _release;
        private AddRefDelegate _addRef;

        protected CallableCOMWrapper(CallableCOMWrapper toClone)
        {
            if (toClone._disposed)
                throw new ObjectDisposedException(GetType().FullName);

            Self = toClone.Self;
            _unknownVTable = toClone._unknownVTable;
            _library = toClone._library;

            AddRef();
            _library.AddRef();
        }

        public int AddRef()
        {
            if (_addRef == null)
                _addRef = (AddRefDelegate)Marshal.GetDelegateForFunctionPointer(_unknownVTable->AddRef, typeof(AddRefDelegate));

            int count = _addRef(Self);
            return count;
        }

        protected CallableCOMWrapper(RefCountedFreeLibrary library, ref Guid desiredInterface, IntPtr pUnknown)
        {
            _library = library;
            _library.AddRef();

            IUnknownVTable* tbl = *(IUnknownVTable**)pUnknown;

            QueryInterfaceDelegate queryInterface = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(tbl->QueryInterface, typeof(QueryInterfaceDelegate));
            int hr = queryInterface(pUnknown, ref desiredInterface, out IntPtr pCorrectUnknown);
            if (hr != 0)
            {
                GC.SuppressFinalize(this);
                throw new InvalidCastException($"{GetType().FullName}.QueryInterface({desiredInterface}) failed, hr=0x{hr:x}");
            }

            ReleaseDelegate release = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(tbl->Release, typeof(ReleaseDelegate));
            int count = release(pUnknown);
            Self = pCorrectUnknown;
            _unknownVTable = *(IUnknownVTable**)pCorrectUnknown;
        }

        public int Release()
        {
            if (_release == null)
                _release = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(_unknownVTable->Release, typeof(ReleaseDelegate));

            int count = _release(Self);
            return count;
        }

        public IntPtr QueryInterface(ref Guid riid)
        {
            QueryInterfaceDelegate queryInterface = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(_unknownVTable->QueryInterface, typeof(QueryInterfaceDelegate));

            int hr = queryInterface(Self, ref riid, out IntPtr unk);
            return hr == S_OK ? unk : IntPtr.Zero;
        }

        protected static bool SUCCEEDED(int hresult)
        {
            return hresult >= 0;
        }

        protected static void InitDelegate<T>(ref T t, IntPtr entry)
            where T : Delegate
        {
            if (t != null)
                return;

            t = (T)Marshal.GetDelegateForFunctionPointer(entry, typeof(T));

#if DEBUG
            if (t.Method.GetParameters().First().ParameterType != typeof(IntPtr))
                throw new InvalidOperationException();
#endif
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Release();
                _library.Release();
                _disposed = true;
            }
        }

        ~CallableCOMWrapper()
        {
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}