using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, Guid("A0647DE9-55DE-4816-929C-385271C64CF7"), InterfaceType(1)]
    public interface ICorDebugStackWalk
    {
        void GetContext([In] uint contextFlags,
                        [In] uint contextBufferSize,
                        [Out] out uint contextSize,
                        [In] IntPtr contextBuffer);

        void SetContext([In] CorDebugSetContextFlag flag,
                        [In] uint contextSize,
                        [In] IntPtr contextBuffer);

        [PreserveSig]
        int Next();

        void GetFrame([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);
    }
}