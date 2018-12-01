// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class LocalVarRoot : ClrRoot
    {
        public LocalVarRoot(ulong addr, ulong obj, ClrType type, ClrAppDomain domain, ClrThread thread, bool pinned, bool falsePos, bool interior, ClrStackFrame stackFrame)
        {
            Address = addr;
            Object = obj;
            IsPinned = pinned;
            IsPossibleFalsePositive = falsePos;
            IsInterior = interior;
            AppDomain = domain;
            Thread = thread;
            Type = type;
            StackFrame = stackFrame;
        }

        public override ClrStackFrame StackFrame { get; }
        public override ClrAppDomain AppDomain { get; }
        public override ClrThread Thread { get; }
        public override bool IsPossibleFalsePositive { get; }
        public override string Name => "local var";
        public override bool IsPinned { get; }
        public override GCRootKind Kind => GCRootKind.LocalVar;
        public override bool IsInterior { get; }
        public override ClrType Type { get; }
    }
}