// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrRoot
    {
        ulong Address { get; }
        bool IsInterior { get; }
        bool IsPinned { get; }
        IClrValue Object { get; }
        ClrRootKind RootKind { get; }
        IClrStackFrame? StackFrame { get; }
    }
}