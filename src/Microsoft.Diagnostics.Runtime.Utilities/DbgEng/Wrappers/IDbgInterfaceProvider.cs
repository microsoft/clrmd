// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    internal interface IDbgInterfaceProvider
    {
        nint DebugClient { get; }
        nint DebugControl { get; }
        nint DebugDataSpaces { get; }
        nint DebugSymbols { get; }
        nint DebugSystemObjects { get; }
        nint DebugAdvanced { get; }
    }
}