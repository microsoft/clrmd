using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_MANRESET : uint
    {
        DEFAULT = 0,
        LOAD_DLL = 1,
    }
}