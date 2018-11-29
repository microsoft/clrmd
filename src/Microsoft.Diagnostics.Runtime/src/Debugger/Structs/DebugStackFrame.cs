using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_STACK_FRAME
    {
        public UInt64 InstructionOffset;
        public UInt64 ReturnOffset;
        public UInt64 FrameOffset;
        public UInt64 StackOffset;
        public UInt64 FuncTableEntry;
        public fixed UInt64 Params[4];
        public fixed UInt64 Reserved[6];
        public UInt32 Virtual;
        public UInt32 FrameNumber;
    }
}