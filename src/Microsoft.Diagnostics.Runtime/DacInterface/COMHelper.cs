using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public abstract class COMHelper
    {
        protected const int S_OK = 0;
        protected const int E_FAIL = unchecked((int)0x80004005);
        protected const int E_NOTIMPL = unchecked((int)0x80004001);
        protected const int E_NOINTERFACE = unchecked((int)0x80004002);

        protected readonly Guid IUnknownGuid = new Guid("00000000-0000-0000-C000-000000000046");

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate int AddRefDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate int ReleaseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate int QueryInterfaceDelegate(IntPtr self, ref Guid guid, out IntPtr ptr);

        public static unsafe int Release(IntPtr pUnk)
        {
            if (pUnk == IntPtr.Zero)
                return 0;

            IUnknownVTable* vtable = *(IUnknownVTable**)pUnk;

            var release = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(vtable->Release, typeof(ReleaseDelegate));
            return release(pUnk);
        }
    }
}