// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ElfFileTableEntryPointers64
    {
        public ulong Start;
        public ulong Stop;
        public ulong PageOffset;
    }
}