using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_STACK_FRAME_EX
    {
        /* DEBUG_STACK_FRAME */
        public ulong InstructionOffset;
        public ulong ReturnOffset;
        public ulong FrameOffset;
        public ulong StackOffset;
        public ulong FuncTableEntry;
        public fixed ulong Params[4];
        public fixed ulong Reserved[6];
        public uint Virtual;
        public uint FrameNumber;

        /* DEBUG_STACK_FRAME_EX */
        public uint InlineFrameContext;
        public uint Reserved1;

        public DEBUG_STACK_FRAME_EX(DEBUG_STACK_FRAME dsf)
        {
            InstructionOffset = dsf.InstructionOffset;
            ReturnOffset = dsf.ReturnOffset;
            FrameOffset = dsf.FrameOffset;
            StackOffset = dsf.StackOffset;
            FuncTableEntry = dsf.FuncTableEntry;
            for (var i = 0; i < 4; ++i)
                Params[i] = dsf.Params[i];
            for (var i = 0; i < 6; ++i)
                Reserved[i] = dsf.Reserved[i];
            Virtual = dsf.Virtual;
            FrameNumber = dsf.FrameNumber;
            InlineFrameContext = 0xFFFFFFFF;
            Reserved1 = 0;
        }
    }
}