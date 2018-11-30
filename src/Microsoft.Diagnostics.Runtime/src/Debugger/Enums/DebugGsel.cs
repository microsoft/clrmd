using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_GSEL : uint
    {
        DEFAULT = 0,
        NO_SYMBOL_LOADS = 1,
        ALLOW_LOWER = 2,
        ALLOW_HIGHER = 4,
        NEAREST_ONLY = 8,
        INLINE_CALLSITE = 0x10
    }
}