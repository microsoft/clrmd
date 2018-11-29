using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_REGISTERS : uint
    {
        DEFAULT = 0,
        INT32 = 1,
        INT64 = 2,
        FLOAT = 4,
        ALL = 7,
    }
}