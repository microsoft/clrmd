// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ElfFileTableEntryPointers32
    {
        public int Start;
        public int Stop;
        public int PageOffset;
    }
}