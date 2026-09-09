// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    // Security regression tests for the temp-DAC cleanup contract. As part of the
    // CLRMD-001 fix, the verified DAC is copied to a per-invocation temp directory
    // and RefCountedFreeLibrary's onRelease callback is what deletes that temp copy
    // when the DAC is unloaded. The callback must fire exactly once, only when the
    // last reference is released, or the temp file leaks. Using IntPtr.Zero avoids
    // any real native FreeLibrary call.
    public class RefCountedFreeLibraryTests
    {
        [Fact]
        public void OnRelease_FiresExactlyOnce_WhenRefCountReachesZero()
        {
            int released = 0;
            RefCountedFreeLibrary library = new(IntPtr.Zero, suppressFree: true, onRelease: () => released++);

            Assert.Equal(0, released);
            Assert.Equal(0, library.Release());
            Assert.Equal(1, released);
        }

        [Fact]
        public void OnRelease_DoesNotFire_WhileReferencesRemain()
        {
            int released = 0;
            RefCountedFreeLibrary library = new(IntPtr.Zero, suppressFree: true, onRelease: () => released++);

            library.AddRef();          // refcount 2
            Assert.Equal(1, library.Release()); // refcount 1
            Assert.Equal(0, released);

            Assert.Equal(0, library.Release()); // refcount 0
            Assert.Equal(1, released);
        }

        [Fact]
        public void OnRelease_FiresOnlyOnce_AcrossBalancedAddRefRelease()
        {
            int released = 0;
            RefCountedFreeLibrary library = new(IntPtr.Zero, suppressFree: true, onRelease: () => released++);

            for (int i = 0; i < 5; i++)
                library.AddRef();

            for (int i = 0; i < 5; i++)
                Assert.NotEqual(0, library.Release());

            Assert.Equal(0, released);
            Assert.Equal(0, library.Release());
            Assert.Equal(1, released);
        }

        [Fact]
        public void Release_WithNullOnRelease_DoesNotThrow()
        {
            RefCountedFreeLibrary library = new(IntPtr.Zero, suppressFree: true);
            Assert.Equal(0, library.Release());
        }
    }
}
