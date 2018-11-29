using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_TBINFO : uint
    {
        NONE = 0,
        EXIT_STATUS = 1,
        PRIORITY_CLASS = 2,
        PRIORITY = 4,
        TIMES = 8,
        START_OFFSET = 0x10,
        AFFINITY = 0x20,
        ALL = 0x3f
    }
}