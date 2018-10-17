using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.ComWrappers
{
    unsafe class CallableCOMWrapper : COMHelper, IDisposable
    {
        private static int _totalInstances;

        public static int TotalInstances => _totalInstances;

        private bool _disposed = false;

        protected IntPtr Self { get; }
        private IUnknownVTable* _unknownVTable;
        protected void* _vtable => _unknownVTable + 1;

        private ReleaseDelegate _release;

        public CallableCOMWrapper(ref Guid desiredInterface, IntPtr pUnknown)
        {
            Interlocked.Increment(ref _totalInstances);
            IUnknownVTable* tbl = *(IUnknownVTable**)pUnknown;

            var queryInterface = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(tbl->QueryInterface, typeof(QueryInterfaceDelegate));
            int hr = queryInterface(pUnknown, ref desiredInterface, out IntPtr pCorrectUnknown);
            if (hr != 0)
                throw new InvalidOperationException();

            var release = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(tbl->Release, typeof(ReleaseDelegate));
            release(pUnknown);

            Self = pCorrectUnknown;
            _unknownVTable = *(IUnknownVTable**)pCorrectUnknown;
        }

        public void Release()
        {
            if (_release == null)
                _release = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(_unknownVTable->Release, typeof(ReleaseDelegate));

            _release(Self);
        }

        public IntPtr QueryInterface(ref Guid riid)
        {
            var queryInterface = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(_unknownVTable->QueryInterface, typeof(QueryInterfaceDelegate));

            int hr = queryInterface(Self, ref riid, out IntPtr unk);
            return hr == S_OK ? unk : IntPtr.Zero;
        }

        protected static bool SUCCEEDED(int hresult) => hresult >= 0;

        protected static void InitDelegate<T>(ref T t, IntPtr entry) where T : Delegate
        {
            if (t != null)
                return;

            t = (T)Marshal.GetDelegateForFunctionPointer(entry, typeof(T));

#if DEBUG
            if (t.Method.GetParameters().First().ParameterType != typeof(IntPtr))
                throw new InvalidOperationException();
#endif
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Release();
                Interlocked.Decrement(ref _totalInstances);
                _disposed = true;
            }
        }

        ~CallableCOMWrapper() => Dispose(false);

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
