using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CorDebugGuidToTypeMapping
    {
        public Guid iid;
        public ICorDebugType icdType;
    }
}