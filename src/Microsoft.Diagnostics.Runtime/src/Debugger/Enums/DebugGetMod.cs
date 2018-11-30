using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_GETMOD : uint
    {
        DEFAULT = 0,
        NO_LOADED_MODULES = 1,
        NO_UNLOADED_MODULES = 2
    }
}