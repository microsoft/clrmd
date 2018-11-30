namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    ///  Gotten from MiniDumpReadDumpStream via streamPointer
    ///  This is a var-args structure defined as:
    ///    ULONG32 NumberOfModules;  
    ///    MINIDUMP_MODULE Modules[];
    /// </summary>
    internal class MINIDUMP_MODULE_LIST : MinidumpArray<MINIDUMP_MODULE>
    {
        internal MINIDUMP_MODULE_LIST(DumpPointer streamPointer)
            : base(streamPointer, MINIDUMP_STREAM_TYPE.ModuleListStream)
        {
        }
    }
}