using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("A69ACAD8-2374-46e9-9FF8-B1F14120D296")]
    public interface ICorDebugHeapValue3
    {
        void GetThreadOwningMonitorLock(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugThread thread,
            [Out] out int acquisitionCount);

        void GetMonitorEventWaitList(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugThreadEnum threadEnum);
    }
}