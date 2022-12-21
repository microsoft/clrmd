namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [Flags]
    public enum DEBUG_ECREATE_PROCESS : uint
    {
        DEFAULT = 0,
        INHERIT_HANDLES = 1,
        USE_VERIFIER_FLAGS = 2,
        USE_IMPLICIT_COMMAND_LINE = 4
    }
}
