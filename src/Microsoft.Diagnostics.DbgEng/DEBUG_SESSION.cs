namespace Microsoft.Diagnostics.DbgEng
{
    public enum DEBUG_SESSION : uint
    {
        ACTIVE = 0,
        END_SESSION_ACTIVE_TERMINATE = 1,
        END_SESSION_ACTIVE_DETACH = 2,
        END_SESSION_PASSIVE = 3,
        END = 4,
        REBOOT = 5,
        HIBERNATE = 6,
        FAILURE = 7
    }
}
