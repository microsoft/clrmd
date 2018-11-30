namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal interface IMinidumpThreadList
    {
        uint Count();
        MINIDUMP_THREAD GetElement(uint idx);
    }
}