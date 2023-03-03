using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.MacOS.Structs
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal readonly struct LoadCommandHeader
    {
        public static unsafe uint HeaderSize => (uint)sizeof(LoadCommandHeader);
        public LoadCommandType Kind { get; }
        public uint Size { get; }
    }
}
