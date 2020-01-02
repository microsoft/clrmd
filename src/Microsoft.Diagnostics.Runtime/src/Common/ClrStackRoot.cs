// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
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

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            ClrThread? thread = StackFrame.Thread;
            if (thread != null)
                builder.AppendFormat(null, "Thread {0:x} ", thread.OSThreadId);

            builder.Append(StackFrame);
            builder.AppendFormat(null, " @{0:x12} {1}", Address, Object);
            return builder.ToString();
        }
    }
}
