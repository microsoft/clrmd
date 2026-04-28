// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal static class CacheNativeMethods
    {
        internal static class Memory
        {
            [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
            internal static extern UIntPtr memcpy(UIntPtr dest, UIntPtr src, UIntPtr count);

            [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
            internal static extern UIntPtr memcpy(IntPtr dest, UIntPtr src, UIntPtr count);
        }
    }
}
