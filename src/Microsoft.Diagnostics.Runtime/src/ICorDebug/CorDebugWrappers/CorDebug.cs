// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [CoClass(typeof(CorDebugClass))]
    [Guid("3D6F5F61-7538-11D3-8D5B-00104B35E7EF")]
    public interface CorDebug : ICorDebug
    {
    }
}