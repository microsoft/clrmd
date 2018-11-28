using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct WDBGEXTS_CLR_DATA_INTERFACE
    {
        public Guid* Iid;
        private void* _iface;

        public WDBGEXTS_CLR_DATA_INTERFACE(Guid* iid)
        {
            Iid = iid;
            _iface = null;
        }

        public object Interface
        {
            get { return (_iface != null) ? Marshal.GetObjectForIUnknown((IntPtr)_iface) : null; }
        }
    }
}