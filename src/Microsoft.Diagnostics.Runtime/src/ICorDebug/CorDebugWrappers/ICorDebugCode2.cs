// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [ComConversionLoss]
    [Guid("5F696509-452F-4436-A3FE-4D11FE7E2347")]
    [InterfaceType(1)]
    public interface ICorDebugCode2
    {
        void GetCodeChunks(
            [In] uint cbufSize,
            [Out] out uint pcnumChunks,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            _CodeChunkInfo[] chunks);

        void GetCompilerFlags([Out] out uint pdwFlags);
    }
}