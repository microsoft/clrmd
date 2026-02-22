// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class SigParserBoundsTests
    {
        [Fact]
        public unsafe void SkipExactlyOne_GenericInstWithExcessiveArgCount_ReturnsFalse()
        {
            // Craft a signature buffer: GENERICINST, CLASS, token, argCount=127, then no data.
            // The parser should return false (not enough data) rather than looping 127 times.
            byte[] sig = new byte[]
            {
                0x15, // ELEMENT_TYPE_GENERICINST
                0x12, // ELEMENT_TYPE_CLASS
                0x01, // compressed token
                0x7F, // argCnt = 127 (but no actual type args follow)
            };

            fixed (byte* pSig = sig)
            {
                SigParser parser = new(new IntPtr(pSig), sig.Length);
                bool result = parser.SkipExactlyOne();
                Assert.False(result);
            }
        }

        [Fact]
        public unsafe void SkipExactlyOne_ArrayWithExcessiveSizeCount_ReturnsFalse()
        {
            // Craft a signature: ARRAY, I4 (element type), rank=2, sizes=100 (but no data).
            byte[] sig = new byte[]
            {
                0x14, // ELEMENT_TYPE_ARRAY
                0x08, // ELEMENT_TYPE_I4
                0x02, // rank = 2
                0x64, // sizes = 100 (but insufficient data follows)
            };

            fixed (byte* pSig = sig)
            {
                SigParser parser = new(new IntPtr(pSig), sig.Length);
                bool result = parser.SkipExactlyOne();
                Assert.False(result);
            }
        }

        [Fact]
        public unsafe void SkipExactlyOne_ValidGenericInst_ReturnsTrue()
        {
            // GENERICINST, CLASS, token, argCount=1, I4
            byte[] sig = new byte[]
            {
                0x15, // ELEMENT_TYPE_GENERICINST
                0x12, // ELEMENT_TYPE_CLASS
                0x01, // compressed token
                0x01, // argCnt = 1
                0x08, // ELEMENT_TYPE_I4 (the type argument)
            };

            fixed (byte* pSig = sig)
            {
                SigParser parser = new(new IntPtr(pSig), sig.Length);
                bool result = parser.SkipExactlyOne();
                Assert.True(result);
            }
        }
    }
}
