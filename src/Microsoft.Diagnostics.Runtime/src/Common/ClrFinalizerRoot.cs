// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a root that comes from the finalizer queue.
    /// </summary>
    public sealed class ClrFinalizerRoot : IClrRoot
    {
        public ulong Address { get; }
        public ClrObject Object { get; }
        public ClrRootKind RootKind => ClrRootKind.FinalizerQueue;
        public bool IsInterior => false;
        public bool IsPinned => false;

        public ClrFinalizerRoot(ulong address, ClrObject obj)
        {
            Address = address;
            Object = obj;
        }

        public override string ToString() => $"finalization root @{Address:x12} -> {Object}";
    }
}
