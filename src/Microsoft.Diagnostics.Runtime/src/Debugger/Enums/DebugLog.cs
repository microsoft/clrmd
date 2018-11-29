using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_LOG : uint
    {
        DEFAULT = 0,
        APPEND = 1,
        UNICODE = 2,
        DML = 4,
    }
}