using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("565005FC-0F8A-4F3E-9EDB-83102B156595")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICorDebugValue3
    {
        void GetSize64(out ulong pSize);
    }
}