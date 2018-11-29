namespace Microsoft.Diagnostics.Runtime.Interop
{
    public enum DEBUG_SYMTYPE : uint
    {
        NONE = 0,
        COFF = 1,
        CODEVIEW = 2,
        PDB = 3,
        EXPORT = 4,
        DEFERRED = 5,
        SYM = 6,
        DIA = 7,
    }
}