// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    public enum CorDebugStateChange
    {
        None = 0,
        PROCESS_RUNNING = 0x0000001,
        FLUSH_ALL = 0x0000002
    }
}