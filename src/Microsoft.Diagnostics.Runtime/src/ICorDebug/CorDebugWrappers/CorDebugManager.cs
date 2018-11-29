using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, Guid("3D6F5F61-7538-11D3-8D5B-00104B35E7EF"), CoClass(typeof(CorDebugManagerClass))]
    public interface CorDebugManager : ICorDebug
    {
    }
}