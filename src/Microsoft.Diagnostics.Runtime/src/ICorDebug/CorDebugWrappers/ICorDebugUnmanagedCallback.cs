using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, Guid("5263E909-8CB5-11D3-BD2F-0000F80849BD"), InterfaceType(1)]
    public interface ICorDebugUnmanagedCallback
    {
        void DebugEvent([In] IntPtr pDebugEvent, [In] int fOutOfBand);
    }
}