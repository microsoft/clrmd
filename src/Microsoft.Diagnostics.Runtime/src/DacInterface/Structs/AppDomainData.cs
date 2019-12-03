// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct AppDomainData
    {
        public readonly ulong Address;
        public readonly ulong SecurityDescriptor;
        public readonly ulong LowFrequencyHeap;
        public readonly ulong HighFrequencyHeap;
        public readonly ulong StubHeap;
        public readonly ulong DomainLocalBlock;
        public readonly ulong DomainLocalModules;
        public readonly int Id;
        public readonly int AssemblyCount;
        public readonly int FailedAssemblyCount;
        public readonly int Stage;
    }
}