using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_OUTSYM : uint
    {
        DEFAULT = 0,
        FORCE_OFFSET = 1,
        SOURCE_LINE = 2,
        ALLOW_DISPLACEMENT = 4
    }
}