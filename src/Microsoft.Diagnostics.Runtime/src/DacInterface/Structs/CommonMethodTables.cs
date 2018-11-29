using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CommonMethodTables
    {
        public readonly ulong ArrayMethodTable;
        public readonly ulong StringMethodTable;
        public readonly ulong ObjectMethodTable;
        public readonly ulong ExceptionMethodTable;
        public readonly ulong FreeMethodTable;

        internal bool Validate()
        {
            return ArrayMethodTable != 0 &&
                StringMethodTable != 0 &&
                ObjectMethodTable != 0 &&
                ExceptionMethodTable != 0 &&
                FreeMethodTable != 0;
        }
    }
}