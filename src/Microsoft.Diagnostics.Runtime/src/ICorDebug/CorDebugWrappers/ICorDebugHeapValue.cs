using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, Guid("CC7BCAFA-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugHeapValue : ICorDebugValue
    {

        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);



        void IsValid([Out] out int pbValid);

        void CreateRelocBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);

    }
}