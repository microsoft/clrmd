using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.MacOS.Structs
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct Segment64LoadCommand
    {
        private fixed byte SegName[16];
        public ulong VMAddr { get; }
        public ulong VMSize { get; }
        public ulong FileOffset { get; }
        public ulong FileSize { get; }
        public uint MaxProt { get; }
        public uint InitProt { get; }
        public uint NumberSections { get; }
        public uint Flags { get; }

        public string Name
        {
            get
            {
                fixed (byte *ptr = SegName)
                    return Encoding.ASCII.GetString(ptr, 16).TrimEnd((char)0);
            }
        }
    }
}
