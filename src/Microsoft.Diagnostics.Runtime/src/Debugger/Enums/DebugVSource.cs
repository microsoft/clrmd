namespace Microsoft.Diagnostics.Runtime.Interop
{
    public enum DEBUG_VSOURCE : uint
    {
        INVALID = 0,
        DEBUGGEE = 1,
        MAPPED_IMAGE = 2,
        DUMP_WITHOUT_MEMINFO = 3
    }
}