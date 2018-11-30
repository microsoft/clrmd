namespace Microsoft.Diagnostics.Runtime.Interop
{
    public enum DEBUG_END : uint
    {
        PASSIVE = 0,
        ACTIVE_TERMINATE = 1,
        ACTIVE_DETACH = 2,
        END_REENTRANT = 3,
        END_DISCONNECT = 4
    }
}