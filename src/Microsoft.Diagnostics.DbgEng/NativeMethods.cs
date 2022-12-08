using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.DbgEng
{
    internal static class NativeMethods
    {
        [DllImport("dbgeng.dll")]
        internal static extern int DebugCreate(in Guid riid, out nint pDebugClient);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern nint LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);
    }
}
