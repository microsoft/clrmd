// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IAppDomainData
    {
        IClrAppDomainHelpers Helpers { get; }
        string? Name { get; }
        int Id { get; }
        ulong Address { get; }
    }
}