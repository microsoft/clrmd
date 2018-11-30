// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("AE4CA65D-59DD-42A2-83A5-57E8A08D8719")]
    [InterfaceType(1)]
    public interface ICorDebugExceptionObjectValue
    {
        void EnumerateExceptionCallStack([Out] out ICorDebugExceptionObjectCallStackEnum ppCallStackEnum);
    }
}