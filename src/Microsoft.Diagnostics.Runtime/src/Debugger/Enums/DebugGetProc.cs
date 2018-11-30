using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_GET_PROC : uint
    {
        DEFAULT = 0,
        FULL_MATCH = 1,
        ONLY_MATCH = 2,
        SERVICE_NAME = 4
    }
}