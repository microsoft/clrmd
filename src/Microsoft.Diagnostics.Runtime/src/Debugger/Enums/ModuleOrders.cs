using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum MODULE_ORDERS : uint
    {
        MASK = 0xF0000000,
        LOADTIME = 0x10000000,
        MODULENAME = 0x20000000,
    }
}