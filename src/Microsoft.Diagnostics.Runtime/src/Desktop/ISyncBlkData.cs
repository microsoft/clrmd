// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface ISyncBlkData
    {
        bool Free { get; }
        ulong Address { get; }
        ulong Object { get; }
        ulong OwningThread { get; }
        bool MonitorHeld { get; }
        uint Recursion { get; }
        uint TotalCount { get; }
    }
}