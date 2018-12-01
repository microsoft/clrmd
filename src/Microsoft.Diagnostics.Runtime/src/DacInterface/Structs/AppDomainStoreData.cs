// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct AppDomainStoreData : IAppDomainStoreData
    {
        public readonly ulong SharedDomain;
        public readonly ulong SystemDomain;
        public readonly int AppDomainCount;

        ulong IAppDomainStoreData.SharedDomain => SharedDomain;
        ulong IAppDomainStoreData.SystemDomain => SystemDomain;
        int IAppDomainStoreData.Count => AppDomainCount;
    }
}