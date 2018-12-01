// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_SEGMENT
    {
        public ulong start; // The start address of the segment.
        public ulong end; // The end address of the segment.
        public CorDebugGenerationTypes type; // The generation of the segment.
        public uint heap; // The heap the segment resides in.
    }
}