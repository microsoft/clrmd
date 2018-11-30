using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential)]
    public struct COR_FIELD
    {
        public int token; // FieldDef token to get the field info
        public int offset; // Offset in object of data.
        public COR_TYPEID id; // TYPEID of the field
        public CorElementType fieldType;
    }
}