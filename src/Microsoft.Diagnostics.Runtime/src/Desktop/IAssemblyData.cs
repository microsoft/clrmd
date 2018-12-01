// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IAssemblyData
    {
        ulong Address { get; }
        ulong ParentDomain { get; }
        ulong AppDomain { get; }
        bool IsDynamic { get; }
        bool IsDomainNeutral { get; }
        int ModuleCount { get; }
    }
}