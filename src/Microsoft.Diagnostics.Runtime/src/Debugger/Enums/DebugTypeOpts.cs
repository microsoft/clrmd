using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_TYPEOPTS : uint
    {
        UNICODE_DISPLAY = 1,
        LONGSTATUS_DISPLAY = 2,
        FORCERADIX_OUTPUT = 4,
        MATCH_MAXSIZE = 8
    }
}