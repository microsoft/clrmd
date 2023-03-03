// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class ClrRefCountedHandle : ClrHandle
    {
        public override uint ReferenceCount { get; }

        public ClrRefCountedHandle(ClrAppDomain parent, ulong address, ClrObject obj, uint refCount)
            : base(parent, address, obj, ClrHandleKind.RefCounted)
        {
            ReferenceCount = refCount;
        }
    }
}
