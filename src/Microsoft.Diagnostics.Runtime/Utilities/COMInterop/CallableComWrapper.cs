// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
namespace Microsoft.Diagnostics.Runtime.Utilities
{
    public unsafe class CallableCOMWrapper : COMHelper, IDisposable
    {
        private bool _disposed;

        protected IntPtr Self { get; }
        private readonly IUnknownVTable* _unknownVTable;

        /// <summary>
        /// Returns the underlying COM interface pointer without adding a reference. The caller must ensure
        /// this wrapper (and thus its reference) outlives any use of the returned pointer, or add its own
        /// reference first. Intended for interop hand-off scenarios.
        /// </summary>
        internal IntPtr DangerousGetHandle() => Self;

        protected void* _vtable => _unknownVTable + 1;

        protected CallableCOMWrapper(CallableCOMWrapper toClone)
        {
            if (toClone is null)
                throw new ArgumentNullException(nameof(toClone));

            if (toClone._disposed)
                throw new ObjectDisposedException(GetType().FullName);

            Self = toClone.Self;
            _unknownVTable = toClone._unknownVTable;

            AddRef();
        }

        public int AddRef()
        {
            int count = _unknownVTable->AddRef(Self);
            return count;
        }

        public void SuppressRelease()
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        protected CallableCOMWrapper(in Guid desiredInterface, IntPtr pUnknown)
        {
            IUnknownVTable* tbl = *(IUnknownVTable**)pUnknown;

            int hr = tbl->QueryInterface(pUnknown, desiredInterface, out IntPtr pCorrectUnknown);
            if (hr != 0)
            {
                GC.SuppressFinalize(this);
                throw new InvalidCastException($"{GetType().FullName}.QueryInterface({desiredInterface}) failed, hr=0x{hr:x}");
            }

            int count = tbl->Release(pUnknown);
            Self = pCorrectUnknown;
            _unknownVTable = *(IUnknownVTable**)pCorrectUnknown;
        }

        public int Release()
        {
            int count = _unknownVTable->Release(Self);
            return count;
        }

        public IntPtr QueryInterface(in Guid riid)
        {
            HResult hr = _unknownVTable->QueryInterface(Self, riid, out IntPtr unk);
            return hr.IsOK ? unk : IntPtr.Zero;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Release();
                }

                _disposed = true;
            }
        }

#if DEBUG
        // COM wrappers must be deterministically disposed. Their lifetime (and, for DAC wrappers, the DAC
        // module that backs them) is owned by ClrRuntime/DataTarget, not by these wrappers. A finalized
        // wrapper therefore indicates a missing Dispose. We intentionally do NOT release the COM interface
        // here: there is no finalizer in release builds, and releasing after the backing module may already
        // have been unloaded would call into freed code. This assert exists purely to catch leaks in debug.
        ~CallableCOMWrapper()
        {
            Debug.Fail($"{GetType().FullName} was not disposed. COM wrappers must be disposed.");
        }
#endif

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}