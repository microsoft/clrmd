// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public readonly struct MethodTableCollectibleData : IMethodTableCollectibleData
    {
        public readonly ulong LoaderAllocatorObjectHandle;
        public readonly uint Collectible;

        ulong IMethodTableCollectibleData.LoaderAllocatorObjectHandle => LoaderAllocatorObjectHandle;
        bool IMethodTableCollectibleData.Collectible => Collectible != 0;
    }
}
