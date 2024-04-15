// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities.DbgEng.Structs;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_READ_USER_MINIDUMP_STREAM
    {
        public MINIDUMP_STREAM_TYPE StreamType;
        public uint Flags;
        public uint Offset;
        public IntPtr Buffer;
        public uint BufferSize;
        public uint BufferUsed;
    }
}