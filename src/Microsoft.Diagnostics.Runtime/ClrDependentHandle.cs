// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class ClrDependentHandle : ClrHandle
    {
        public override ClrObject Dependent { get; }

        public ClrDependentHandle(ClrAppDomain parent, ulong address, ClrObject obj, ClrObject dependent)
            : base(parent, address, obj, ClrHandleKind.Dependent)
        {
            Dependent = dependent;
        }
    }
}
