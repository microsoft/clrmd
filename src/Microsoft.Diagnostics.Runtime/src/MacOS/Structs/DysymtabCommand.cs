﻿using Microsoft.Diagnostics.Runtime.MacOS;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.MacOS.Structs
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    readonly struct DysymtabCommand
    {
        public LoadCommandType cmd { get; }
        public uint cmdsize { get; }
        public uint ilocalsym { get; }
        public uint nlocalsym { get; }
        public uint iextdefsym { get; }
        public uint nextdefsym { get; }
        public uint iundefsym { get; }
        public uint nundefsym { get; }
        public uint tocoff { get; }
        public uint ntoc { get; }
        public uint modtaboff { get; }
        public uint nmodtab { get; }
        public uint extrefsymoff { get; }
        public uint nextrefsyms { get; }
        public uint indirectsymoff { get; }
        public uint nindirectsyms { get; }
        public uint extreloff { get; }
        public uint nextrel { get; }
        public uint locreloff { get; }
        public uint nlocrel { get; }
    }
}
