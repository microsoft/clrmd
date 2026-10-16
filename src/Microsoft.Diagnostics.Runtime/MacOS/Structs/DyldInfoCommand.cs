// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.MacOS.Structs
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal readonly struct DyldInfoCommand
    {
        public LoadCommandHeader Header { get; }
        public uint RebaseOffset { get; }
        public uint RebaseSize { get; }
        public uint BindOffset { get; }
        public uint BindSize { get; }
        public uint WeakBindOffset { get; }
        public uint WeakBindSize { get; }
        public uint LazyBindOffset { get; }
        public uint LazyBindSize { get; }
        public uint ExportOffset { get; }
        public uint ExportSize { get; }
    }
}
