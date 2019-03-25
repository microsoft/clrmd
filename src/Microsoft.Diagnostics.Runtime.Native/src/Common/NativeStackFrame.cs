// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Native
{
    internal class NativeStackFrame : ClrStackFrame
    {
        public NativeStackFrame(ulong ip, string symbolName, ClrModule module, byte[] context)
        {
            InstructionPointer = ip;
            DisplayString = symbolName;
            Module = module;
            Context = context;
        }

        public override string DisplayString { get; }
        public ClrModule Module { get; }
        public override ulong InstructionPointer { get; }
        public override byte[] Context { get; }
        public override ClrStackFrameType Kind => ClrStackFrameType.Unknown;
        public override ClrMethod Method => null;
        public override ulong StackPointer => 0;
        public override ClrThread Thread => null;
    }
}