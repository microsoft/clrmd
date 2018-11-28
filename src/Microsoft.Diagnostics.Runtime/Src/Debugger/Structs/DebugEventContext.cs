using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_EVENT_CONTEXT
    {
        public uint Size;
        public uint ProcessEngineId;
        public uint ThreadEngineId;
        public uint FrameEngineId;
    }
}