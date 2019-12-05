// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class PointerHelpers
    {
        public static IntPtr AsIntPtr(this ulong address)
        {
            if (IntPtr.Size == 8)
                return new IntPtr((long)address);

            return new IntPtr((int)address);
        }
    }
}
