using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("CC7BCAEB-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugValueBreakpoint : ICorDebugBreakpoint
    {
        new void Activate([In] int bActive);

        new void IsActive([Out] out int pbActive);

        void GetValue(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);
    }
}