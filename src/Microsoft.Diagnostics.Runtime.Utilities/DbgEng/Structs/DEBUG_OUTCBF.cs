// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    /// <summary>
    /// First argument ('Which') to <c>IDebugOutputCallbacks2::Output2</c>.
    /// Tells the receiver what kind of payload is being delivered.
    /// Values from dbgeng.h DEBUG_OUTCB_*.
    /// </summary>
    public enum DEBUG_OUTCB : uint
    {
        TEXT = 0,
        DML = 1,
        EXPLICIT_FLUSH = 2,
    }

    /// <summary>
    /// Second argument ('Flags') to <c>IDebugOutputCallbacks2::Output2</c>.
    /// Bitfield describing properties of the payload. Values from dbgeng.h
    /// DEBUG_OUTCBF_*.
    /// </summary>
    [Flags]
    public enum DEBUG_OUTCBF : uint
    {
        NONE = 0,
        COMBINED_EXPLICIT_FLUSH = 0x01,
        DML_HAS_TAGS = 0x02,
        DML_HAS_SPECIAL_CHARACTERS = 0x04,
    }
}