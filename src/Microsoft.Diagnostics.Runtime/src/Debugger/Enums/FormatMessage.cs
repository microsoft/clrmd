using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum FORMAT_MESSAGE
    {
        ALLOCATE_BUFFER = 0x0100,
        IGNORE_INSERTS = 0x0200,
        FROM_STRING = 0x0400,
        FROM_HMODULE = 0x0800,
        FROM_SYSTEM = 0x1000,
        ARGUMENT_ARRAY = 0x2000
    }
}