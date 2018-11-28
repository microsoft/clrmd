namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    public enum CorDebugBlockingReason
    {
        None = 0,
        MonitorCriticalSection = 1,
        MonitorEvent = 2
    }
}