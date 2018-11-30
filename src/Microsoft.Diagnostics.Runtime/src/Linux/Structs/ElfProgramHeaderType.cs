namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal enum ElfProgramHeaderType : uint
    {
        Null = 0,
        Load = 1,
        Dynamic = 2,
        Interp = 3,
        Note = 4,
        Shlib = 5,
        Phdr = 6
    }
}