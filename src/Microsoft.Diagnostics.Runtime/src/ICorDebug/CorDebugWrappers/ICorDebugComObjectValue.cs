// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("5F69C5E5-3E12-42DF-B371-F9D761D6EE24")]
    [InterfaceType(1)]
    public interface ICorDebugComObjectValue
    {
        void GetCachedInterfaceTypes([In] bool bIInspectableOnly, [Out] out ICorDebugTypeEnum ppInterfacesEnum);
    }
}