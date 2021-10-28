using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.MacOS.Structs
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    readonly struct SymtabLoadCommand
    {
        public LoadCommandHeader Header { get; }
        public uint SymOff { get; }
        public uint NSyms { get; }
        public uint StrOff { get; }
        public uint StrSize { get; }
    }
}
