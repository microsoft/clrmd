namespace Microsoft.Diagnostics.Runtime.Interop
{
    public enum DEBUG_MODNAME : uint
    {
        IMAGE = 0x00000000,
        MODULE = 0x00000001,
        LOADED_IMAGE = 0x00000002,
        SYMBOL_FILE = 0x00000003,
        MAPPED_IMAGE = 0x00000004
    }
}