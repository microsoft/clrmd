namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V2ThreadPoolData : IThreadPoolData
    {
        private int _numQueuedWorkRequests;
        private uint _numTimers;
        private int _numCPThreads;
        private int _numRetiredCPThreads;
        private int _currentLimitTotalCPThreads;

        public int MinCP { get; }
        public int MaxCP { get; }
        public int CPU { get; }
        public int NumFreeCP { get; }
        public int MaxFreeCP { get; }
        public int TotalThreads { get; }
        public int RunningThreads { get; }
        public int IdleThreads { get; }
        public int MinThreads { get; }
        public int MaxThreads { get; }
        ulong IThreadPoolData.FirstWorkRequest { get; }
        public ulong QueueUserWorkItemCallbackFPtr { get; }
        public ulong AsyncCallbackCompletionFPtr { get; }
        public ulong AsyncTimerCallbackCompletionFPtr { get; }
    }
}