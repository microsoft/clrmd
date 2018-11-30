using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_FIND_SOURCE : uint
    {
        DEFAULT = 0,
        FULL_PATH = 1,
        BEST_MATCH = 2,
        NO_SRCSRV = 4,
        TOKEN_LOOKUP = 8
    }
}