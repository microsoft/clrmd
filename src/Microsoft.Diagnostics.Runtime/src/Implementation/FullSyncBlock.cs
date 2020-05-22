// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class FullSyncBlock : SyncBlock
    {
        public SyncBlockComFlags ComFlags { get; }

        public override bool IsComCallWrapper => (ComFlags & SyncBlockComFlags.ComCallableWrapper) == SyncBlockComFlags.ComCallableWrapper;
        public override bool IsRuntimeCallWrapper => (ComFlags & SyncBlockComFlags.ComCallableWrapper) == SyncBlockComFlags.ComCallableWrapper;
        public override bool IsComClassFactory => (ComFlags & SyncBlockComFlags.ComClassFactory) == SyncBlockComFlags.ComClassFactory;

        public override bool IsMonitorHeld { get; }
        public override ulong HoldingThreadAddress { get; }
        public override int RecursionCount { get; }
        public override int WaitingThreadCount { get; }

        public FullSyncBlock(in SyncBlockData syncBlk)
            : base(syncBlk.Object)
        {
            ComFlags = (SyncBlockComFlags)syncBlk.COMFlags;

            IsMonitorHeld = syncBlk.MonitorHeld != 0;
            HoldingThreadAddress = syncBlk.HoldingThread;
            RecursionCount = syncBlk.Recursion >= int.MaxValue ? int.MaxValue : (int)syncBlk.Recursion;
            WaitingThreadCount = (int)syncBlk.AdditionalThreadCount;
        }
    }
}
