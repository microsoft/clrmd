using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential)]
    public struct COR_GC_REFERENCE
    {
        public ICorDebugAppDomain Domain;
        public ICorDebugValue Location;
        public CorGCReferenceType Type;
        public ulong ExtraData;
    }
}