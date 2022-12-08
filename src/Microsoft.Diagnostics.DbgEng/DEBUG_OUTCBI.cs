namespace Microsoft.Diagnostics.DbgEng
{
    [Flags]
    public enum DEBUG_OUTCBI : uint
    {
        NONE = 0,
        EXPLICIT_FLUSH = 1,
        TEXT = 2,
        DML = 4,
    }
}