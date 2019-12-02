using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a 
    /// </summary>
    public struct ClrStackRoot : IClrRoot
    {
        public ulong Address { get; }
        public ClrObject Object { get; }
        public ClrStackFrame StackFrame { get; }
        public ClrRootKind RootKind => ClrRootKind.Stack;
        public bool IsInterior { get; }
        public bool IsPinned { get; }

        public ClrStackRoot(ulong address, ClrObject obj, ClrStackFrame stackFrame, bool interior, bool pinned)
        {
            Address = address;
            Object = obj;
            StackFrame = stackFrame;
            IsInterior = interior;
            IsPinned = pinned;
        }
    }
}
