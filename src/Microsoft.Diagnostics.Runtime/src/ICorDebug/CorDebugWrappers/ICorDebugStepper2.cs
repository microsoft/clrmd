// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("C5B6E9C3-E7D1-4A8E-873B-7F047F0706F7")]
    [InterfaceType(1)]
    public interface ICorDebugStepper2
    {
        void SetJMC([In] int fIsJMCStepper);
    }
}