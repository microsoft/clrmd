namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    internal interface IDebugSystemObjects
    {
        public int ProcessSystemId { get; }
        public int CurrentThreadId { get; set; }

        public int GetCurrentThreadTeb(out ulong teb);
        public int GetNumberThreads(out int threadCount);
    }
}
