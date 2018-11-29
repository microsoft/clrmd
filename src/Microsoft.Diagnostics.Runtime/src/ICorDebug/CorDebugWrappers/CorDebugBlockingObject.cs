namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    public struct CorDebugBlockingObject
    {
        public ICorDebugValue BlockingObject;
        public uint Timeout;
        public CorDebugBlockingReason BlockingReason;
    }
}