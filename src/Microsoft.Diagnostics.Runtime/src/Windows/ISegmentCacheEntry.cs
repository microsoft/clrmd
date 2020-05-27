// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal interface ISegmentCacheEntry
    {
        uint CurrentSize { get; }

        uint MinSize { get; }

        long LastAccessTickCount { get; }

        int AccessCount { get; }

        bool PageOutData();

        void UpdateLastAccessTickCount();

        void IncrementAccessCount();

        void GetDataForAddress(ulong address, uint byteCount, IntPtr buffer, out uint bytesRead);

        bool GetDataFromAddressUntil(ulong address, byte[] terminatingSequence, out byte[] result);
    }
}
