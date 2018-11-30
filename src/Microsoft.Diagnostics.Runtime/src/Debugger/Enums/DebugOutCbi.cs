using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_OUTCBI : uint
    {
        EXPLICIT_FLUSH = 1,
        TEXT = 2,
        DML = 4,
        ANY_FORMAT = 6
    }
}