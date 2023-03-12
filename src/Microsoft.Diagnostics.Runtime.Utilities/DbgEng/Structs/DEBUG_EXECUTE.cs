// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [Flags]
    public enum DEBUG_EXECUTE : uint
    {
        DEFAULT = 0,
        ECHO = 1,
        NOT_LOGGED = 2,
        NO_REPEAT = 4
    }
}