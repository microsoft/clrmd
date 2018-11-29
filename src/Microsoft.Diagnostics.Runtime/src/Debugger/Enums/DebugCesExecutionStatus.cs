using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_CES_EXECUTION_STATUS : ulong
    {
        INSIDE_WAIT = 0x100000000UL,
        WAIT_TIMEOUT = 0x200000000UL,
    }
}