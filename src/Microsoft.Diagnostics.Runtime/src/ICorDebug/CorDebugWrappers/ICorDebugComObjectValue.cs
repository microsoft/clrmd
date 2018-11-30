using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("5F69C5E5-3E12-42DF-B371-F9D761D6EE24")]
    [InterfaceType(1)]
    public interface ICorDebugComObjectValue
    {
        void GetCachedInterfaceTypes([In] bool bIInspectableOnly, [Out] out ICorDebugTypeEnum ppInterfacesEnum);
    }
}