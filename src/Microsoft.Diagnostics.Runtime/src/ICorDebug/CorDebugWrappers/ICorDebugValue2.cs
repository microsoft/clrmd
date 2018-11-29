using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, Guid("5E0B54E7-D88A-4626-9420-A691E0A78B49"), InterfaceType(1)]
    public interface ICorDebugValue2
    {

        void GetExactType([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugType ppType);
    }
}