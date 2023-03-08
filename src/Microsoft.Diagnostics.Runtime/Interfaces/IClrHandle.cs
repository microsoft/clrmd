// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrHandle : IClrRoot
    {
        IClrAppDomain AppDomain { get; }
        IClrValue Dependent { get; }
        ClrHandleKind HandleKind { get; }
        bool IsStrong { get; }
        uint ReferenceCount { get; }
    }
}