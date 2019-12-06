// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface ITypeHelpers
    {
        IDataReader DataReader { get; }
        ITypeFactory Factory { get; }
        IClrObjectHelpers ClrObjectHelpers { get; }

        string? GetTypeName(ulong mt);
        ulong GetLoaderAllocatorHandle(ulong mt);

        // TODO: Should not expose this:
        IObjectData GetObjectData(ulong objRef);
    }
}