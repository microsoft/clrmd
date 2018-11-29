using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_ACTIVE_FUNCTION
    {
        public ICorDebugAppDomain pAppDomain;
        public ICorDebugModule pModule;
        public ICorDebugFunction2 pFunction;
        public uint ilOffset;
        public uint Flags;
    }
}