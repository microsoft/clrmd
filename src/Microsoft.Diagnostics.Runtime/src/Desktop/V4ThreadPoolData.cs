namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V4ThreadPoolData : IThreadPoolData
    {
        private uint _useNewWorkerPool;

        private int _numRetiredWorkerThreads;

        private ulong _hillClimbingLog;
        private int _hillClimbingLogFirstIndex;
        private int _hillClimbingLogSize;

        private uint _numTimers;

        private int _numCPThreads;
        private int _numRetiredCPThreads;
        private int _currentLimitTotalCPThreads;

        private ulong _queueUserWorkItemCallbackFPtr;
        private ulong _asyncCallbackCompletionFPtr;
        private ulong _asyncTimerCallbackCompletionFPtr;

        public int MinCP { get; }
        public int MaxCP { get; }
        public int CPU { get; }
        public int NumFreeCP { get; }
        public int MaxFreeCP { get; }
        public int TotalThreads { get; }
        public int RunningThreads => TotalThreads + IdleThreads + _numRetiredWorkerThreads;
        public int IdleThreads { get; }
        public int MinThreads { get; }
        public int MaxThreads { get; }
        public ulong FirstWorkRequest { get; }
        ulong IThreadPoolData.QueueUserWorkItemCallbackFPtr => ulong.MaxValue;
        ulong IThreadPoolData.AsyncCallbackCompletionFPtr => ulong.MaxValue;
        ulong IThreadPoolData.AsyncTimerCallbackCompletionFPtr => ulong.MaxValue;
    }
}