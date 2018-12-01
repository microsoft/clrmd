// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("2EE06488-C0D4-42B1-B26D-F3795EF606FB")]
    [InterfaceType(1)]
    [ComConversionLoss]
    public interface ICorDebugProcess3
    {
        void SetEnableCustomNotification(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugClass pClass,
            [In] int fOnOff);
    }
}