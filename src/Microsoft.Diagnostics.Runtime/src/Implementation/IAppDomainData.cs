﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal interface IAppDomainData
    {
        IAppDomainHelpers Helpers { get; }
        string? Name { get; }
        int Id { get; }
        ulong Address { get; }
    }
}