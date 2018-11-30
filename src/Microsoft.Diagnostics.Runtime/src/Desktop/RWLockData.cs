// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct RWLockData : IRWLockData
    {
        public IntPtr pNext;
        public IntPtr pPrev;
        public int _uLockID;
        public int _lLockID;
        public short wReaderLevel;

        public ulong Next => (ulong)pNext.ToInt64();
        public int ULockID => _uLockID;
        public int LLockID => _lLockID;
        public int Level => wReaderLevel;
    }
}