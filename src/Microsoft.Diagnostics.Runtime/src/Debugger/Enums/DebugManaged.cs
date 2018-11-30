using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_MANAGED : uint
    {
        DISABLED = 0,
        ALLOWED = 1,
        DLL_LOADED = 2
    }
}