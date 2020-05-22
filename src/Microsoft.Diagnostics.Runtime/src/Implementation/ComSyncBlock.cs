// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ComSyncBlock : SyncBlock
    {
        public SyncBlockComFlags ComFlags { get; }

        public override bool IsComCallWrapper => (ComFlags & SyncBlockComFlags.ComCallableWrapper) == SyncBlockComFlags.ComCallableWrapper;
        public override bool IsRuntimeCallWrapper => (ComFlags & SyncBlockComFlags.ComCallableWrapper) == SyncBlockComFlags.ComCallableWrapper;
        public override bool IsComClassFactory => (ComFlags & SyncBlockComFlags.ComClassFactory) == SyncBlockComFlags.ComClassFactory;

        public ComSyncBlock(ulong obj, uint comFlags)
            : base(obj)
        {
            ComFlags = (SyncBlockComFlags)comFlags;
        }
    }
}
