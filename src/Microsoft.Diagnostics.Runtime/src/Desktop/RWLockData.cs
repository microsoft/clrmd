// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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