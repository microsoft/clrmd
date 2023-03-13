// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    internal static class NativeMethods
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
        [DllImport("dbgeng.dll")]
        internal static extern int DebugCreate(in Guid riid, out nint pDebugClient);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern nint LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);
    }
}