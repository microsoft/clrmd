using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CorDebugGuidToTypeMapping
    {
        public System.Guid iid;
        public ICorDebugType icdType;
    }
}