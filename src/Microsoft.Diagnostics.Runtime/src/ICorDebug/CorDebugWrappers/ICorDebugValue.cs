using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("CC7BCAF7-8A68-11D2-983C-0000F808342D")]
    [InterfaceType(1)]
    public interface ICorDebugValue
    {
        void GetType([Out] out CorElementType pType);

        void GetSize([Out] out uint pSize);

        void GetAddress([Out] out ulong pAddress);

        void CreateBreakpoint(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValueBreakpoint ppBreakpoint);
    }
}