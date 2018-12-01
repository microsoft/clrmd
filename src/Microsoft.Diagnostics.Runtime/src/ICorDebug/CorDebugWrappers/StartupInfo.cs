// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 8)]
    [ComVisible(false)]
    public class STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public SafeFileHandle hStdInput;
        public SafeFileHandle hStdOutput;
        public SafeFileHandle hStdError;

        public STARTUPINFO()
        {
            // Initialize size field.
            cb = Marshal.SizeOf(this);

            // initialize safe handles 
            hStdInput = new SafeFileHandle(new IntPtr(0), false);
            hStdOutput = new SafeFileHandle(new IntPtr(0), false);
            hStdError = new SafeFileHandle(new IntPtr(0), false);
        }
    }
}