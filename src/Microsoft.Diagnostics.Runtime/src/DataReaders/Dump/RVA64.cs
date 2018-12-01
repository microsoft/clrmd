// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// RVAs are offsets into the minidump.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RVA64
    {
        public ulong Value;
    }
}