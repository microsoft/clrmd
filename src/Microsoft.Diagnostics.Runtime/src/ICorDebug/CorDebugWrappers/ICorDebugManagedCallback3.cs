// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("264EA0FC-2591-49AA-868E-835E6515323F")]
    [InterfaceType(1)]
    public interface ICorDebugManagedCallback3
    {
        void CustomNotification(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain);
    }
}