// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("86F012BF-FF15-4372-BD30-B6F11CAAE1DD")]
    public interface ICorDebugModule3
    {
        void CreateReaderForInMemorySymbols(
            [In][ComAliasName("REFIID")] ref Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out object ppObj);
    }
}