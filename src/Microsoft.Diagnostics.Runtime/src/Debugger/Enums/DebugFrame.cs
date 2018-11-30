using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_FRAME : uint
    {
        DEFAULT = 0,
        IGNORE_INLINE = 1
    }
}