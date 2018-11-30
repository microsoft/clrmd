using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_CONNECT_SESSION : uint
    {
        DEFAULT = 0,
        NO_VERSION = 1,
        NO_ANNOUNCE = 2
    }
}