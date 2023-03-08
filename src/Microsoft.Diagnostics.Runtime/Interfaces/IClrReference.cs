// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrReference
    {
        IClrInstanceField? Field { get; }
        IClrReference? InnerField { get; }
        bool IsArrayElement { get; }
        bool IsDependentHandle { get; }
        bool IsField { get; }
        IClrValue Object { get; }
        int Offset { get; }
    }
}