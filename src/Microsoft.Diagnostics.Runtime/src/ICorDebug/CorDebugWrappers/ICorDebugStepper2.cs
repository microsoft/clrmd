using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport, Guid("C5B6E9C3-E7D1-4A8E-873B-7F047F0706F7"), InterfaceType(1)]
    public interface ICorDebugStepper2
    {

        void SetJMC([In] int fIsJMCStepper);
    }
}