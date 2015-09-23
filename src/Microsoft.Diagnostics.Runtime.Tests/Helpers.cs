
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Diagnostics.Runtime.Utilities;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    class Helpers
    {
        private const string Kernel32LibraryName = "kernel32.dll";
        [DllImportAttribute(Kernel32LibraryName, SetLastError = true)]
        public static extern IntPtr LoadLibraryEx(String fileName, int hFile, LoadLibraryFlags dwFlags);

        [Flags]
        public enum LoadLibraryFlags : uint
        {
            NoFlags = 0x00000000,
            DontResolveDllReferences = 0x00000001,
            LoadIgnoreCodeAuthzLevel = 0x00000010,
            LoadLibraryAsDatafile = 0x00000002,
            LoadLibraryAsDatafileExclusive = 0x00000040,
            LoadLibraryAsImageResource = 0x00000020,
            LoadWithAlteredSearchPath = 0x00000008
        }
    }
}