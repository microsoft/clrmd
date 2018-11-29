using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VS_FIXEDFILEINFO
    {
        public UInt32 dwSignature;
        public UInt32 dwStrucVersion;
        public UInt32 dwFileVersionMS;
        public UInt32 dwFileVersionLS;
        public UInt32 dwProductVersionMS;
        public UInt32 dwProductVersionLS;
        public UInt32 dwFileFlagsMask;
        public VS_FF dwFileFlags;
        public UInt32 dwFileOS;
        public UInt32 dwFileType;
        public UInt32 dwFileSubtype;
        public UInt32 dwFileDateMS;
        public UInt32 dwFileDateLS;
    }
}