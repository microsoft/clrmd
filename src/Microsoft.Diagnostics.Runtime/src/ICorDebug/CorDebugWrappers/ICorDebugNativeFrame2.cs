using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("35389FF1-3684-4c55-A2EE-210F26C60E5E")]
    public interface ICorDebugNativeFrame2
    {
        void IsChild([Out] out int pChild);

        void IsMatchingParentFrame(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugNativeFrame2 pFrame,
            [Out] out int pParent);

        void GetCalleeStackParameterSize([Out] out uint pSize);
    }
}