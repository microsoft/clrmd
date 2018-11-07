// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class SafeLoadLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeLoadLibraryHandle() : base(true) { }
        public SafeLoadLibraryHandle(IntPtr handle)
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return FreeLibrary(handle);
        }
        
        [DllImportAttribute("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FreeLibrary(IntPtr hModule);

        // This is technically equivalent to DangerousGetHandle, but it's safer for loaded
        // libraries where the HMODULE is also the base address the module is loaded at.
        public IntPtr BaseAddress
        {
            get
            {
                return handle;
            }
        }
    }
}