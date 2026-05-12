// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Tracks bytes allocated against a configured cap. Returns <see langword="false"/>
    /// from <see cref="TryReserve"/> once the cap is hit so that callers can stop
    /// growing buffers and surface a <c>LimitExceeded</c> diagnostic, bounding
    /// peak working-set while parsing arbitrarily large stress logs.
    /// </summary>
    internal sealed class AllocationBudget
    {
        private readonly long _cap;
        private long _consumed;

        public AllocationBudget(long capBytes)
        {
            _cap = capBytes;
        }

        public bool TryReserve(int bytes)
        {
            if (bytes < 0)
                return false;

            long next = _consumed + bytes;
            if (next < _consumed || next > _cap)
                return false;

            _consumed = next;
            return true;
        }

        public void Release(int bytes)
        {
            if (bytes <= 0)
                return;

            long next = _consumed - bytes;
            _consumed = next < 0 ? 0 : next;
        }

        public long Consumed => _consumed;
        public long Cap => _cap;
    }
}
