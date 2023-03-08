using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrDelegate
    {
        bool HasMultipleTargets { get; }
        IClrValue Object { get; }

        IEnumerable<IClrDelegateTarget> EnumerateDelegateTargets();
        IClrDelegateTarget? GetDelegateTarget();
    }
}