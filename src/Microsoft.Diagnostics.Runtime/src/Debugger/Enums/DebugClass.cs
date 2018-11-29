namespace Microsoft.Diagnostics.Runtime.Interop
{
    public enum DEBUG_CLASS : uint
    {
        UNINITIALIZED = 0,
        KERNEL = 1,
        USER_WINDOWS = 2,
        IMAGE_FILE = 3,
    }
}