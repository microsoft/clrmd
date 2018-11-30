namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    public enum CorDebugInternalFrameType
    {
        STUBFRAME_NONE,
        STUBFRAME_M2U,
        STUBFRAME_U2M,
        STUBFRAME_APPDOMAIN_TRANSITION,
        STUBFRAME_LIGHTWEIGHT_FUNCTION,
        STUBFRAME_FUNC_EVAL,
        STUBFRAME_INTERNALCALL,
        STUBFRAME_CLASS_INIT,
        STUBFRAME_EXCEPTION,
        STUBFRAME_SECURITY,
        STUBFRAME_JIT_COMPILATION
    }
}