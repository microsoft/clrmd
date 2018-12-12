// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    [StructLayout(LayoutKind.Sequential)]
    // Same for v2 and v4
    internal struct LegacyModuleMapTraverseArgs
    {
        private readonly uint _setToZero;
        public ulong Module;
        public IntPtr Callback;
        public IntPtr Token;
    }
}