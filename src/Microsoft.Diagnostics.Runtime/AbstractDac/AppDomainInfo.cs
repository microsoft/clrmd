// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal struct AppDomainInfo
    {
        public ulong Address { get; set; }
        public AppDomainKind Kind { get; set; }

        public int Id { get; set; }
        public string Name { get; set; }

        public string? ConfigFile { get; set; }
        public string? ApplicationBase { get; set; }

        public ulong LoaderAllocator { get; set; }
    }

    internal enum AppDomainKind
    {
        Normal,
        System,
        Shared,
    }
}