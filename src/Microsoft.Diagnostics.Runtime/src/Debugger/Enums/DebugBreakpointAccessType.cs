using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_BREAKPOINT_ACCESS_TYPE : uint
    {
        READ = 1,
        WRITE = 2,
        EXECUTE = 4,
        IO = 8
    }
}