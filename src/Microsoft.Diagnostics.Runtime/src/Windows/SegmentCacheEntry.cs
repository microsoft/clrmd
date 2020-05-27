// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    abstract class SegmentCacheEntry
    {
        public uint CurrentSize { get; protected set; }

        public uint MinSize { get; protected set; }

        public abstract long LastAccessTickCount { get; }

        public abstract int AccessCount { get; }

        public abstract bool PageOutData();

        public abstract void UpdateLastAccessTickCount();

        public abstract void IncrementAccessCount();

        public abstract void GetDataForAddress(ulong address, uint byteCount, IntPtr buffer, out uint bytesRead);

        public abstract bool GetDataFromAddressUntil(ulong address, byte[] terminatingSequence, out byte[] result);
    }
}
