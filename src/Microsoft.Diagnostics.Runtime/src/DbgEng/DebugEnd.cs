namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal enum DebugEnd
    {
        Passive,
        ActiveTerminate,
        ActiveDetatch,
        EndReentrant,
        EndDisconnect            
    }
}
