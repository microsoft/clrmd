// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct AppDomainData
    {
        public readonly ClrDataAddress Address;
        public readonly ClrDataAddress SecurityDescriptor;
        public readonly ClrDataAddress LowFrequencyHeap;
        public readonly ClrDataAddress HighFrequencyHeap;
        public readonly ClrDataAddress StubHeap;
        public readonly ClrDataAddress DomainLocalBlock;
        public readonly ClrDataAddress DomainLocalModules;
        public readonly int Id;
        public readonly int AssemblyCount;
        public readonly int FailedAssemblyCount;
        public readonly int Stage;
    }
}