// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct MINIDUMP_HEADER
    {
        public readonly uint Singature;
        public readonly uint Version;
        public readonly uint NumberOfStreams;
        public readonly uint StreamDirectoryRva;
        public readonly uint CheckSum;
        public readonly uint TimeDateStamp;
        public readonly ulong Flags;
    }
}