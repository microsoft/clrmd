using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, InterfaceType(1), Guid("E3AC4D6C-9CB7-43E6-96CC-B21540E5083C")]
    public interface ICorDebugHeapValue2
    {

        void CreateHandle([In] CorDebugHandleType type, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugHandleValue ppHandle);
    }
}