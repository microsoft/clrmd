// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class MINIDUMP_THREAD_EX : MINIDUMP_THREAD
    {
        public override bool HasBackingStore()
        {
            return true;
        }

        public override MINIDUMP_MEMORY_DESCRIPTOR BackingStore { get; set; }
    }
}