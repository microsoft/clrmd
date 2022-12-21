namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [Flags]
    public enum DEBUG_EXECUTE : uint
    {
        DEFAULT = 0,
        ECHO = 1,
        NOT_LOGGED = 2,
        NO_REPEAT = 4
    }
}
