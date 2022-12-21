﻿namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [Flags]
    public enum DEBUG_OUTPUT : uint
    {
        NORMAL = 1,
        ERROR = 2,
        WARNING = 4,
        VERBOSE = 8,
        PROMPT = 0x10,
        PROMPT_REGISTERS = 0x20,
        EXTENSION_WARNING = 0x40,
        DEBUGGEE = 0x80,
        DEBUGGEE_PROMPT = 0x100,
        SYMBOLS = 0x200,

        ALL = NORMAL | ERROR | WARNING | VERBOSE | PROMPT | PROMPT_REGISTERS | EXTENSION_WARNING | DEBUGGEE | DEBUGGEE_PROMPT | SYMBOLS,
    }
}
