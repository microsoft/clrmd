using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [Flags]
    public enum IMAGE_FILE
    {
        RELOCS_STRIPPED = 0x0001,
        EXECUTABLE_IMAGE = 0x0002,
        LINE_NUMS_STRIPPED = 0x0004,
        LOCAL_SYMS_STRIPPED = 0x0008,
        LARGE_ADDRESS_AWARE = 0x0020,
        _32BIT_MACHINE = 0x0100,
        DEBUG_STRIPPED = 0x0200,
        REMOVABLE_RUN_FROM_SWAP = 0x0400,
        NET_RUN_FROM_SWAP = 0x0800,
        SYSTEM = 0x1000,
        DLL = 0x2000,
        UP_SYSTEM_ONLY = 0x4000
    }
}
