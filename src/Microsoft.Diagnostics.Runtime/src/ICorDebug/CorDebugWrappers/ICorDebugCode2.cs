using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, ComConversionLoss, Guid("5F696509-452F-4436-A3FE-4D11FE7E2347"), InterfaceType(1)]
    public interface ICorDebugCode2
    {

        void GetCodeChunks([In] uint cbufSize, [Out] out uint pcnumChunks, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] _CodeChunkInfo[] chunks);

        void GetCompilerFlags([Out] out uint pdwFlags);
    }
}