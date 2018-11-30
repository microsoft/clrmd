namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal enum ElfNoteType
    {
        PrpsStatus = 1,
        PrpsFpreg = 2,
        PrpsInfo = 3,
        TASKSTRUCT = 4,
        Aux = 6,

        File = 0x46494c45 // "FILE" in ascii
    }
}