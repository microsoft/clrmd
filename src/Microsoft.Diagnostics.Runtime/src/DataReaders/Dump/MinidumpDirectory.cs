// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct MINIDUMP_DIRECTORY
    {
        public readonly MINIDUMP_STREAM_TYPE StreamType;
        public readonly uint DataSize;
        public readonly uint Rva;
    }
}