using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_STACK_FRAME
    {
        public ulong InstructionOffset;
        public ulong ReturnOffset;
        public ulong FrameOffset;
        public ulong StackOffset;
        public ulong FuncTableEntry;
        public fixed ulong Params[4];
        public fixed ulong Reserved[6];
        public uint Virtual;
        public uint FrameNumber;
    }
}