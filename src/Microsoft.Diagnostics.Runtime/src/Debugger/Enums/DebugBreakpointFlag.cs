using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_BREAKPOINT_FLAG : uint
    {
        GO_ONLY = 1,
        DEFERRED = 2,
        ENABLED = 4,
        ADDER_ONLY = 8,
        ONE_SHOT = 0x10
    }
}