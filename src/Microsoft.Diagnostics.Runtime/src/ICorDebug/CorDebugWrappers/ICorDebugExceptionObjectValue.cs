using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("AE4CA65D-59DD-42A2-83A5-57E8A08D8719")]
    [InterfaceType(1)]
    public interface ICorDebugExceptionObjectValue
    {
        void EnumerateExceptionCallStack([Out] out ICorDebugExceptionObjectCallStackEnum ppCallStackEnum);
    }
}