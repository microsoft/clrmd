using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("1A1F204B-1C66-4637-823F-3EE6C744A69C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICorDebugThread4
    {
        [PreserveSig]
        int HasUnhandledException();

        void GetBlockingObjects(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugEnumBlockingObject blockingObjectEnumerator);

        void GetCurrentCustomDebuggerNotification(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppNotificationObject);
    }
}