using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CorDebugExceptionObjectStackFrame
    {
        public ICorDebugModule pModule;
        public ulong ip;
        public int methodDef;
        public bool isLastForeignException;
    }
}