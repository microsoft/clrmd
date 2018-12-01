// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [Flags]
    public enum CorDebugMDAFlags
    {
        // Fields
        None = 0,
        MDA_FLAG_SLIP = 2
    }
}