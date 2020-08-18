// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdDependentHandle : ClrHandle
    {
        public override ulong Address { get; }

        public override ClrObject Object { get; }

        public override ClrHandleKind HandleKind => ClrHandleKind.Dependent;

        public override uint ReferenceCount => uint.MaxValue;

        public override ClrObject Dependent { get; }

        public override ClrAppDomain AppDomain { get; }

        public ClrmdDependentHandle(ClrAppDomain parent, ulong address, ClrObject obj, ClrObject dependent)
        {
            Address = address;
            Object = obj;
            AppDomain = parent;
            Dependent = dependent;
        }
    }
}
