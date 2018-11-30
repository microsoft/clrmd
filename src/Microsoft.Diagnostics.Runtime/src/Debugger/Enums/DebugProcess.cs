using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_PROCESS : uint
    {
        DEFAULT = 0,
        DETACH_ON_EXIT = 1,
        ONLY_THIS_PROCESS = 2
    }
}