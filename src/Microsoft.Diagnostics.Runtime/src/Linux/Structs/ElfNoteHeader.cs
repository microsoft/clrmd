using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ElfNoteHeader
    {
        public uint NameSize;
        public uint ContentSize;
        public ElfNoteType Type;
    }
}