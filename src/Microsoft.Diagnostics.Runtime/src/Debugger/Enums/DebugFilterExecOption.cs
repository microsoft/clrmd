namespace Microsoft.Diagnostics.Runtime.Interop
{
    public enum DEBUG_FILTER_EXEC_OPTION : uint
    {
        BREAK = 0x00000000,
        SECOND_CHANCE_BREAK = 0x00000001,
        OUTPUT = 0x00000002,
        IGNORE = 0x00000003,
        REMOVE = 0x00000004,
    }
}