namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct DacpWorkRequestData
    {
        public WorkRequestFunctionTypes FunctionType;
        public ulong Function;
        public ulong Context;
        public ulong NextWorkRequest;
    }
}