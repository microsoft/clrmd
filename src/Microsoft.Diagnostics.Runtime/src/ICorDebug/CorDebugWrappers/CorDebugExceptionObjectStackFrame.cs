using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CorDebugExceptionObjectStackFrame
    {
        public ICorDebugModule pModule;
        public UInt64 ip;
        public int methodDef;
        public bool isLastForeignException;
    }
}