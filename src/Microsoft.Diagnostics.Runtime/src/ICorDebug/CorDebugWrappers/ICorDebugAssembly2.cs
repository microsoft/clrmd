using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, InterfaceType(1), Guid("426D1F9E-6DD4-44C8-AEC7-26CDBAF4E398")]
    public interface ICorDebugAssembly2
    {

        void IsFullyTrusted([Out] out int pbFullyTrusted);
    }
}