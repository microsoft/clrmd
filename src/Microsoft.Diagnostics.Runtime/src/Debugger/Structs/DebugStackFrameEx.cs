using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_STACK_FRAME_EX
    {
        /* DEBUG_STACK_FRAME */
        public UInt64 InstructionOffset;
        public UInt64 ReturnOffset;
        public UInt64 FrameOffset;
        public UInt64 StackOffset;
        public UInt64 FuncTableEntry;
        public fixed UInt64 Params[4];
        public fixed UInt64 Reserved[6];
        public UInt32 Virtual;
        public UInt32 FrameNumber;

        /* DEBUG_STACK_FRAME_EX */
        public UInt32 InlineFrameContext;
        public UInt32 Reserved1;

        public DEBUG_STACK_FRAME_EX(DEBUG_STACK_FRAME dsf)
        {
            InstructionOffset = dsf.InstructionOffset;
            ReturnOffset = dsf.ReturnOffset;
            FrameOffset = dsf.FrameOffset;
            StackOffset = dsf.StackOffset;
            FuncTableEntry = dsf.FuncTableEntry;
            fixed (UInt64* pParams = Params)
            {
                for (int i = 0; i < 4; ++i)
                    pParams[i] = dsf.Params[i];
            }
            fixed (UInt64* pReserved = Reserved)
            {
                for (int i = 0; i < 6; ++i)
                    pReserved[i] = dsf.Reserved[i];
            }
            Virtual = dsf.Virtual;
            FrameNumber = dsf.FrameNumber;
            InlineFrameContext = 0xFFFFFFFF;
            Reserved1 = 0;
        }
    }
}