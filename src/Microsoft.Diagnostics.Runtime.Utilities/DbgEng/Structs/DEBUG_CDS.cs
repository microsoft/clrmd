// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [Flags]
    public enum DEBUG_CDS : uint
    {
        ALL = 0xffffffff,
        REGISTERS = 1,
        DATA = 2,
        REFRESH = 4 // Inform the GUI clients to refresh debugger windows.
    }
}