// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class SafeMapViewHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeMapViewHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return UnmapViewOfFile(handle);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnmapViewOfFile(IntPtr baseAddress);

        // This is technically equivalent to DangerousGetHandle, but it's safer for file
        // mappings. In file mappings, the "handle" is actually a base address that needs
        // to be used in computations and RVAs.
        // So provide a safer accessor method.
        public IntPtr BaseAddress
        {
            get
            {
                return handle;
            }
        }
    }
}