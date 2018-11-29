using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 8), ComVisible(false)]
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
            this.cb = Marshal.SizeOf(this);

            // initialize safe handles 
            this.hStdInput = new Win32.SafeHandles.SafeFileHandle(new IntPtr(0), false);
            this.hStdOutput = new Win32.SafeHandles.SafeFileHandle(new IntPtr(0), false);
            this.hStdError = new Win32.SafeHandles.SafeFileHandle(new IntPtr(0), false);
        }
    }
}