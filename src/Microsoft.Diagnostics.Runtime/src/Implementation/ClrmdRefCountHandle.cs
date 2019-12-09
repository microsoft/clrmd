// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdRefCountHandle : ClrHandle
    {
        public override ulong Address { get; }

        public override ClrObject Object { get; }

        public override ClrHandleKind HandleKind => ClrHandleKind.RefCount;

        public override uint ReferenceCount { get; }

        public override ClrObject Dependent => default;

        public override ClrAppDomain AppDomain { get; }

        public ClrmdRefCountHandle(ClrAppDomain parent, ulong address, ClrObject obj, uint refCount)
        {
            Address = address;
            Object = obj;
            AppDomain = parent;
            ReferenceCount = refCount;
        }
    }
}
