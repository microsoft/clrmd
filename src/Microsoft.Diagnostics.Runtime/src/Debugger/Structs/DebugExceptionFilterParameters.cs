using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_EXCEPTION_FILTER_PARAMETERS
    {
        public DEBUG_FILTER_EXEC_OPTION ExecutionOption;
        public DEBUG_FILTER_CONTINUE_OPTION ContinueOption;
        public uint TextSize;
        public uint CommandSize;
        public uint SecondCommandSize;
        public uint ExceptionCode;
    }
}