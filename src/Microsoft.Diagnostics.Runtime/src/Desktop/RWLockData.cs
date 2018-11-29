// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using Microsoft.Diagnostics.Runtime.ICorDebug;
using System.Threading;

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct RWLockData : IRWLockData
    {
        public IntPtr pNext;
        public IntPtr pPrev;
        public int _uLockID;
        public int _lLockID;
        public Int16 wReaderLevel;

        public ulong Next
        {
            get { return (ulong)pNext.ToInt64(); }
        }

        public int ULockID
        {
            get { return _uLockID; }
        }

        public int LLockID
        {
            get { return _lLockID; }
        }


        public int Level
        {
            get { return wReaderLevel; }
        }
    }
}
