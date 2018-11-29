using System;
using System.Security.Permissions;
using System.Reflection;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    class ProcessSafeHandle : Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        private ProcessSafeHandle()
            : base(true)
        {
        }

        private ProcessSafeHandle(IntPtr handle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(handle);
        }
        
        override protected bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }
}
