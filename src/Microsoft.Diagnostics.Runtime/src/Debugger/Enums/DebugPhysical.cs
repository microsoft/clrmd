namespace Microsoft.Diagnostics.Runtime.Interop
{
    public enum DEBUG_PHYSICAL : uint
    {
        DEFAULT = 0,
        CACHED = 1,
        UNCACHED = 2,
        WRITE_COMBINED = 3,
    }
}