// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [Flags]
    public enum DEBUG_OUTCBI : uint
    {
        NONE = 0,
        EXPLICIT_FLUSH = 1,
        TEXT = 2,
        DML = 4,
    }
}