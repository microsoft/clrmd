using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, InterfaceType(1), Guid("C0815BDC-CFAB-447e-A779-C116B454EB5B")]
    public interface ICorDebugInternalFrame2
    {
        void GetAddress([Out] out ulong pAddress);

        void IsCloserToLeaf([In, MarshalAs(UnmanagedType.Interface)] ICorDebugFrame pFrameToCompare,
                            [Out] out int pIsCloser);
    }
}