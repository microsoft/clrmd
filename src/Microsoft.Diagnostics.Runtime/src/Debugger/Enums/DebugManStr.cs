using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_MANSTR : uint
    {
        NONE = 0,
        LOADED_SUPPORT_DLL = 1,
        LOAD_STATUS = 2,
    }
}