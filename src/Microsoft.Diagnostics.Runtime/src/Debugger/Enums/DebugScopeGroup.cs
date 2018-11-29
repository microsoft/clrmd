using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_SCOPE_GROUP : uint
    {
        ARGUMENTS = 1,
        LOCALS = 2,
        ALL = 3,
    }
}