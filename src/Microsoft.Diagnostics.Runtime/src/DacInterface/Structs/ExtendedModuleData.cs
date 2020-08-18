// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public struct ExtendedModuleData
    {
        public int IsDynamic;
        public int IsInMemory;
        public int IsFlatLayout;
        public ClrDataAddress PEFile;
        public ClrDataAddress LoadedPEAddress;
        public ulong LoadedPESize; // size of file on disk
        public ClrDataAddress InMemoryPdbAddress;
        public ulong InMemoryPdbSize;
    }
}
