// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [Flags]
    public enum DEBUG_CREATE_PROCESS : uint
    {
        DEFAULT = 0,
        DEBUG_PROCESS = 0x00000001, /* DEBUG_PROCESS */
        DEBUG_ONLY_THIS_PROCESS = 0x00000002, /* DEBUG_ONLY_THIS_PROCESS */
        NO_DEBUG_HEAP = 0x00000400, /* CREATE_UNICODE_ENVIRONMENT */
        THROUGH_RTL = 0x00010000 /* STACK_SIZE_PARAM_IS_A_RESERVATION */
    }
}