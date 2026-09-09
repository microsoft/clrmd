// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class RefCountedFreeLibrary
    {
        private readonly IntPtr _library;
        private readonly bool _suppressFree;
        private readonly Action? _onRelease;
        private int _refCount;

        public RefCountedFreeLibrary(IntPtr library, bool suppressFree = false, Action? onRelease = null)
        {
            _library = library;
            _suppressFree = suppressFree;
            _onRelease = onRelease;
            _refCount = 1;
        }

        public int AddRef()
        {
            return Interlocked.Increment(ref _refCount);
        }

        public int Release()
        {
            int count = Interlocked.Decrement(ref _refCount);
            if (count == 0)
            {
                if (_library != IntPtr.Zero && !_suppressFree)
                    DataTarget.PlatformFunctions.FreeLibrary(_library);

                _onRelease?.Invoke();
            }

            return count;
        }
    }
}
