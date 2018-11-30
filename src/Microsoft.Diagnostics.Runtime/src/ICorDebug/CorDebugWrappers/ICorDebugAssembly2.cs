// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("426D1F9E-6DD4-44C8-AEC7-26CDBAF4E398")]
    public interface ICorDebugAssembly2
    {
        void IsFullyTrusted([Out] out int pbFullyTrusted);
    }
}