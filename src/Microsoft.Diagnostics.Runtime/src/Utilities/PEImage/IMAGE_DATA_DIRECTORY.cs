// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct IMAGE_DATA_DIRECTORY
    {
        public int VirtualAddress { get; }
        public uint Size { get; }

        public override string ToString() => $"{VirtualAddress:x} size:{Size:x}";
    }
}
