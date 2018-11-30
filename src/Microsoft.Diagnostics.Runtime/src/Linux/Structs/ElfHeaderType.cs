namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal enum ElfHeaderType : ushort
    {
        Relocatable = 1,
        Executable = 2,
        Shared = 3,
        Core = 4
    }
}