// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [ComConversionLoss]
    [InterfaceType(1)]
    [Guid("E930C679-78AF-4953-8AB7-B0AABF0F9F80")]
    public interface ICorDebugProcess4
    {
        void Filter(
            [In] IntPtr pRecord,
            [In] uint countBytes,
            [In] CorDebugRecordFormat format,
            [In] CorDebugFilterFlagsWindows dwFlags,
            [In] uint dwThreadId,
            [In] ICorDebugManagedCallback pCallback,
            [In][Out] ref uint dwContinueStatus);

        void ProcessStateChanged([In] CorDebugStateChange eChange);
    }
}