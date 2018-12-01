// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IMethodTableData
    {
        uint Token { get; }
        bool Shared { get; }
        bool Free { get; }
        bool ContainsPointers { get; }
        uint BaseSize { get; }
        uint ComponentSize { get; }
        ulong EEClass { get; }
        ulong Parent { get; }
        uint NumMethods { get; }
        ulong ElementTypeHandle { get; }
        ulong Module { get; }
    }
}