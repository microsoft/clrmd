using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, Guid("49E4A320-4A9B-4ECA-B105-229FB7D5009F"), InterfaceType(1)]
    public interface ICorDebugObjectValue2
    {

        void GetVirtualMethodAndType([In] uint memberRef, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugType ppType);
    }
}