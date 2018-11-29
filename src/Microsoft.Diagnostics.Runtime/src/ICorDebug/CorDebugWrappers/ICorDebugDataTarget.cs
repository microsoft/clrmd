using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, InterfaceType(1), Guid("FE06DC28-49FB-4636-A4A3-E80DB4AE116C")]
    public interface ICorDebugDataTarget
    {
        CorDebugPlatform GetPlatform();

        uint ReadVirtual(System.UInt64 address,
                         IntPtr buffer,
                         uint bytesRequested);

        void GetThreadContext(uint threadId,
                              uint contextFlags,
                              uint contextSize,
                              IntPtr context);
    }
}