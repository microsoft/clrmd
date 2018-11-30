// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("2BD956D9-7B07-4BEF-8A98-12AA862417C5")]
    [ComConversionLoss]
    [InterfaceType(1)]
    public interface ICorDebugThread2
    {
        void GetActiveFunctions(
            [In] uint cFunctions,
            [Out] out uint pcFunctions,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            COR_ACTIVE_FUNCTION[] pFunctions);

        void GetConnectionID([Out] out uint pdwConnectionId);

        void GetTaskID([Out] out ulong pTaskId);

        void GetVolatileOSThreadID([Out] out uint pdwTid);

        void InterceptCurrentException(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFrame pFrame);
    }
}