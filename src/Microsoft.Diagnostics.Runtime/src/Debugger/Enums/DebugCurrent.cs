using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_CURRENT : uint
    {
        DEFAULT = 0xf,
        SYMBOL = 1,
        DISASM = 2,
        REGISTERS = 4,
        SOURCE_LINE = 8
    }
}