// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Diagnostics.Runtime.Native
{
    internal class NativeStackFrame : ClrStackFrame
    {
        public NativeStackFrame(ulong ip, string symbolName, ClrModule module)
        {
            InstructionPointer = ip;
            DisplayString = symbolName;
            Module = module;
        }

        public override string DisplayString { get; }
        public ClrModule Module { get; }
        public override ulong InstructionPointer { get; }
        public override ClrStackFrameType Kind => ClrStackFrameType.Unknown;
        public override ClrMethod Method => null;
        public override ulong StackPointer => 0;
        public override ClrThread Thread => null;
    }
}