// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
    public readonly struct ClrStackRoot : IClrStackRoot
    {
        public ulong Address { get; }
        public ClrObject Object { get; }
        public ClrStackFrame StackFrame { get; }
        public ClrRootKind RootKind => ClrRootKind.Stack;
        public bool IsInterior => false;
        public bool IsPinned { get; }

        public ClrStackRoot(ulong address, ClrObject obj, ClrStackFrame stackFrame, bool pinned)
        {
            Address = address;
            Object = obj;
            StackFrame = stackFrame;
            IsPinned = pinned;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            ClrThread? thread = StackFrame.Thread;
            if (thread != null)
                builder.Append($"Thread {thread.OSThreadId:x} ");

            builder.Append(StackFrame);
            builder.Append($" @{Address:x12} -> {Object}");
            return builder.ToString();
        }
    }
}
