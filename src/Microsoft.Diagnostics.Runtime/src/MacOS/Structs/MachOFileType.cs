namespace Microsoft.Diagnostics.Runtime.MacOS
{
    internal enum MachOFileType
    {
        Object = 1,
        Execute = 2,
        FVMLib = 3,
        Core = 4,
        Preload = 5,
        Dylib = 6,
        Dylinker = 7,
        Bundle = 8,
        DylibStub = 9,
        DSym = 0xa,
        KExtBundle = 0xb,
    }
}