// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    public interface IClrEnum
    {
        ClrElementType ElementType { get; }
        IClrType Type { get; }

        IEnumerable<(string Name, object? Value)> EnumerateValues();
        IEnumerable<string> GetEnumNames();
        T GetEnumValue<T>(string name) where T : unmanaged;
    }
}