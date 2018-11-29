using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_EXCEPTION_FILTER_PARAMETERS
    {
        public DEBUG_FILTER_EXEC_OPTION ExecutionOption;
        public DEBUG_FILTER_CONTINUE_OPTION ContinueOption;
        public UInt32 TextSize;
        public UInt32 CommandSize;
        public UInt32 SecondCommandSize;
        public UInt32 ExceptionCode;
    }
}