// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.AbstractDac;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class SyncBlockContainer : IEnumerable<SyncBlock>
    {
        private readonly SyncBlock[] _syncBlocks;
        private readonly Dictionary<ulong, SyncBlock> _mapping = new();

        public int Count => _syncBlocks.Length;
        public SyncBlock this[int index] => _syncBlocks[index];
        public SyncBlock this[uint index] => _syncBlocks[index];

        public SyncBlockContainer(IEnumerable<SyncBlockInfo> syncBlocks)
        {
            _syncBlocks = syncBlocks.Select(CreateSyncBlock).ToArray();
            foreach (SyncBlock item in _syncBlocks)
            {
                if (item.Object != 0)
                    _mapping[item.Object] = item;
            }
        }

        private SyncBlock CreateSyncBlock(SyncBlockInfo data)
        {
            if (data.MonitorHeldCount != 0 || data.HoldingThread != 0 || data.Recursion != 0 || data.AdditionalThreadCount != 0)
                return new FullSyncBlock(data);
            else if (data.COMFlags != 0)
                return new ComSyncBlock(data.Object, data.Index, data.COMFlags);
            else
                return new SyncBlock(data.Object, data.Index);
        }

        public SyncBlock? TryGetSyncBlock(ulong obj)
        {
            _mapping.TryGetValue(obj, out SyncBlock? result);
            return result;
        }

        public IEnumerator<SyncBlock> GetEnumerator()
        {
            return ((IEnumerable<SyncBlock>)_syncBlocks).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _syncBlocks.GetEnumerator();
        }
    }
}