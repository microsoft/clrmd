// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct RWLockData : IRWLockData
    {
        public readonly IntPtr Next;
        public readonly IntPtr Prev;
        public readonly int ULockID;
        public readonly int LLockID;
        public readonly short ReaderLevel;

        ulong IRWLockData.Next => (ulong)Next.ToInt64();
        int IRWLockData.ULockID => ULockID;
        int IRWLockData.LLockID => LLockID;
        int IRWLockData.Level => ReaderLevel;
    }
}