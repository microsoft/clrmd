using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrDelegate
    {
        bool HasMultipleTargets { get; }
        ClrObject Object { get; }

        IEnumerable<ClrDelegateTarget> EnumerateDelegateTargets();
        ClrDelegateTarget? GetDelegateTarget();
    }
}