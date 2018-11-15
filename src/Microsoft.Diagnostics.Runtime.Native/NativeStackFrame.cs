// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Diagnostics.Runtime.Native
{
    internal class NativeStackFrame : ClrStackFrame
    {
        private ulong _ip;
        private string _symbolName;
        private ClrModule _module;

        public NativeStackFrame(ulong ip, string symbolName, ClrModule module)
        {
            _ip = ip;
            _symbolName = symbolName;
            _module = module;
        }

        public override string DisplayString
        {
            get
            {
                return _symbolName;
            }
        }

        public ClrModule Module
        {
            get
            {
                return _module;
            }
        }

        public override ulong InstructionPointer
        {
            get
            {
                return _ip;
            }
        }

        public override ClrStackFrameType Kind
        {
            get
            {
                return ClrStackFrameType.Unknown;
            }
        }

        public override ClrMethod Method
        {
            get
            {
                return null;
            }
        }

        public override ulong StackPointer
        {
            get
            {
                return 0;
            }
        }

        public override ClrThread Thread
        {
            get
            {
                return null;
            }
        }
    }
}
