// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    public enum CorDebugChainReason
    {
        // Fields
        CHAIN_CLASS_INIT = 1,
        CHAIN_CONTEXT_POLICY = 8,
        CHAIN_CONTEXT_SWITCH = 0x400,
        CHAIN_DEBUGGER_EVAL = 0x200,
        CHAIN_ENTER_MANAGED = 0x80,
        CHAIN_ENTER_UNMANAGED = 0x100,
        CHAIN_EXCEPTION_FILTER = 2,
        CHAIN_FUNC_EVAL = 0x800,
        CHAIN_INTERCEPTION = 0x10,
        CHAIN_NONE = 0,
        CHAIN_PROCESS_START = 0x20,
        CHAIN_SECURITY = 4,
        CHAIN_THREAD_START = 0x40
    }
}