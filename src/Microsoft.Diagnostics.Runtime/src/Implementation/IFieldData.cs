// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IFieldData
    {
        IFieldHelpers Helpers { get; }

        ClrElementType ElementType { get; }
        int Token { get; }
        int Offset { get; }
        ulong TypeMethodTable { get; }
    }
}