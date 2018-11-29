using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_REGISTER : uint
    {
        SUB_REGISTER = 1,
    }
}