// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class LocalVarRoot : ClrRoot
    {
        private bool _pinned;
        private bool _falsePos;
        private bool _interior;
        private ClrThread _thread;
        private ClrType _type;
        private ClrAppDomain _domain;
        private ClrStackFrame _stackFrame;

        public LocalVarRoot(ulong addr, ulong obj, ClrType type, ClrAppDomain domain, ClrThread thread, bool pinned, bool falsePos, bool interior, ClrStackFrame stackFrame)
        {
            Address = addr;
            Object = obj;
            _pinned = pinned;
            _falsePos = falsePos;
            _interior = interior;
            _domain = domain;
            _thread = thread;
            _type = type;
            _stackFrame = stackFrame;
        }

        public override ClrStackFrame StackFrame
        {
            get
            {
                return _stackFrame;
            }
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return _domain;
            }
        }

        public override ClrThread Thread
        {
            get
            {
                return _thread;
            }
        }

        public override bool IsPossibleFalsePositive
        {
            get
            {
                return _falsePos;
            }
        }

        public override string Name
        {
            get
            {
                return "local var";
            }
        }

        public override bool IsPinned
        {
            get
            {
                return _pinned;
            }
        }

        public override GCRootKind Kind
        {
            get
            {
                return GCRootKind.LocalVar;
            }
        }

        public override bool IsInterior
        {
            get
            {
                return _interior;
            }
        }

        public override ClrType Type
        {
            get { return _type; }
        }
    }
}
