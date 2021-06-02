using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.MacOS.Structs
{
    readonly struct NList64
    {
        public uint n_strx { get; }
        public byte n_type { get; }
        public byte n_sect { get; }
        public ushort n_desc { get; }
        public ulong n_value { get; }
    }
}
