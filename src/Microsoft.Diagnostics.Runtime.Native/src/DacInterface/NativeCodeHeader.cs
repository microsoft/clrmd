namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public readonly struct NativeCodeHeader
    {
        public readonly ulong GCInfo;
        public readonly ulong EHInfo;
        public readonly ulong MethodStart;
        public readonly uint MethodSize;
    }
}