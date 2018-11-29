namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public readonly struct NativeThreadStoreData
    {
        public readonly int ThreadCount;
        public readonly ulong FirstThread;
        public readonly ulong FinalizerThread;
        public readonly ulong GCThread;
    }
}