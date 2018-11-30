using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EXCEPTION_RECORD64
    {
        public uint ExceptionCode;
        public uint ExceptionFlags;
        public ulong ExceptionRecord;
        public ulong ExceptionAddress;
        public uint NumberParameters;
        public uint __unusedAlignment;
        public fixed ulong ExceptionInformation[15]; //EXCEPTION_MAXIMUM_PARAMETERS
    }
}