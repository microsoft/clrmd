// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_DEBUG_IL_TO_NATIVE_MAP
    {
        public uint ilOffset;
        public uint nativeStartOffset;
        public uint nativeEndOffset;
    }
}