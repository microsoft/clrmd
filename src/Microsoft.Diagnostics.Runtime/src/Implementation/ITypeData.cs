// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface ITypeData
    {
        bool IsShared { get; }
        bool ContainsPointers { get; }
        int Token { get; }
        ulong MethodTable { get; }
        // Currently no runtime emits this, but opportunistically I'd like to see it work.
        ulong ComponentMethodTable { get; }
        int BaseSize { get; }
        int ComponentSize { get; }
        int MethodCount { get; }

        ITypeHelpers Helpers { get; }
    }
}