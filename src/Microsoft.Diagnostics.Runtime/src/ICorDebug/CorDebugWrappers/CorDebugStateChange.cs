namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    public enum CorDebugStateChange
    {
        None = 0,
        PROCESS_RUNNING = 0x0000001,
        FLUSH_ALL = 0x0000002
    }
}