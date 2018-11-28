using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct COMInterfacePointerData
    {
        public readonly ulong MethodTable;
        public readonly ulong InterfacePointer;
        public readonly ulong ComContext;
    }
}