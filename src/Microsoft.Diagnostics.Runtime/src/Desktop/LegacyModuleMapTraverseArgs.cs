using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    [StructLayout(LayoutKind.Sequential)]
    // Same for v2 and v4
    internal struct LegacyModuleMapTraverseArgs
    {
        private readonly uint _setToZero;
        public ulong module;
        public IntPtr pCallback;
        public IntPtr token;
    }
}