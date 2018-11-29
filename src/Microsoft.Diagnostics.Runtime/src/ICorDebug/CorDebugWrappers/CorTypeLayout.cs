using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential)]
    public struct COR_TYPE_LAYOUT
    {
        public COR_TYPEID parentID;
        public int objectSize;
        public int numFields;
        public int boxOffset;
        public CorElementType type;
    };
}