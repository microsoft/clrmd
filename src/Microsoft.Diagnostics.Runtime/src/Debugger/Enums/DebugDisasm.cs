using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum DEBUG_DISASM : uint
    {
        EFFECTIVE_ADDRESS = 1,
        MATCHING_SYMBOLS = 2,
        SOURCE_LINE_NUMBER = 4,
        SOURCE_FILE_NAME = 8
    }
}