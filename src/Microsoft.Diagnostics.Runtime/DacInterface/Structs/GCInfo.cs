// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GCInfo
    {
        public readonly int ServerMode;
        public readonly int GCStructuresValid;
        public readonly int HeapCount;
        public readonly int MaxGeneration;
    }
}