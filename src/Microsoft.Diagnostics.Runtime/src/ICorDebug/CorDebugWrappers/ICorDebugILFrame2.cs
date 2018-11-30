using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("5D88A994-6C30-479B-890F-BCEF88B129A5")]
    public interface ICorDebugILFrame2
    {
        void RemapFunction([In] uint newILOffset);

        void EnumerateTypeParameters(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugTypeEnum ppTyParEnum);
    }
}