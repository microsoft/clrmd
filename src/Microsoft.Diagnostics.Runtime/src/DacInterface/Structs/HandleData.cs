using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct HandleData
    {
        public readonly ulong AppDomain;
        public readonly ulong Handle;
        public readonly ulong Secondary;
        public readonly uint Type;
        public readonly uint StrongReference;

        // For RefCounted Handles
        public readonly uint RefCount;
        public readonly uint JupiterRefCount;
        public readonly uint IsPegged;
    }
}