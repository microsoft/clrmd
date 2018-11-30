using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    /// <summary>
    /// Wrapper for standard COM IEnumUnknown, needed for ICLRMetaHost enumeration APIs.
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000100-0000-0000-C000-000000000046")]
    internal interface IEnumUnknown
    {
        [PreserveSig]
        int Next(
            [In][MarshalAs(UnmanagedType.U4)] int celt,
            [Out][MarshalAs(UnmanagedType.IUnknown)]
            out object rgelt,
            IntPtr pceltFetched);

        [PreserveSig]
        int Skip(
            [In][MarshalAs(UnmanagedType.U4)] int celt);

        void Reset();

        void Clone(
            [Out] out IEnumUnknown ppenum);
    }
}