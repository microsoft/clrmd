// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Exercises the ICLRSymbolProvider COM thunks that DacDataTargetCOM exposes,
    /// using an in-memory <see cref="IClrSymbolProvider"/> mock supplied via
    /// <see cref="DataTargetOptions.SymbolProvider"/>. No DAC or crash dump is loaded;
    /// the test only QIs the CCW and invokes the vtable directly.
    /// </summary>
    public unsafe class ClrSymbolProviderTests
    {
        // ICLRSymbolProvider vtable layout (after the 3 IUnknown slots):
        //   slot 3: HRESULT TryGetSymbolName(ulong address, uint cchName, char* pName, uint* pcchNameActual, ulong* pDisplacement);
        //   slot 4: HRESULT TryGetSymbolAddress(LPCWSTR name, ulong* pAddress);
        private const int VtblSlot_TryGetSymbolName = 3;
        private const int VtblSlot_TryGetSymbolAddress = 4;

        [Fact]
        public void TryGetSymbolName_NullProvider_ReturnsNotImpl()
        {
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider: null);

            char buffer = '\0';
            uint actual = 0;
            ulong displacement = 0;
            int hr = h.TryGetSymbolName(0x1000, cchName: 1, &buffer, &actual, &displacement);

            Assert.Equal(HResult.E_NOTIMPL, hr);
        }

        [Fact]
        public void TryGetSymbolName_Success_FillsBufferAndOutParams()
        {
            RecordingSymbolProvider provider = new()
            {
                NameResult = "coreclr!SymbolFoo",
                DisplacementResult = 0x42,
                NameLookupResult = true,
            };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            const int bufLen = 64;
            char* buffer = stackalloc char[bufLen];
            uint actual = 0;
            ulong displacement = 0;

            int hr = h.TryGetSymbolName(0x1234, cchName: bufLen, buffer, &actual, &displacement);

            Assert.Equal(HResult.S_OK, hr);
            Assert.Equal(0x1234ul, provider.LastAddress);
            Assert.Equal(0x42ul, displacement);
            Assert.Equal((uint)provider.NameResult!.Length + 1, actual);
            Assert.Equal(provider.NameResult, new string(buffer));
        }

        [Fact]
        public void TryGetSymbolName_BufferTooSmall_TruncatesWithNullTerminatorAndReturnsSFalse()
        {
            RecordingSymbolProvider provider = new()
            {
                NameResult = "abcdefghij",
                DisplacementResult = 0,
                NameLookupResult = true,
            };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            const int bufLen = 5;
            char* buffer = stackalloc char[bufLen];
            for (int i = 0; i < bufLen; i++) buffer[i] = 'Z';
            uint actual = 0;

            int hr = h.TryGetSymbolName(0x10, cchName: bufLen, buffer, &actual, null);

            Assert.Equal(HResult.S_FALSE, hr);
            Assert.Equal((uint)provider.NameResult!.Length + 1, actual);
            Assert.Equal('\0', buffer[bufLen - 1]);
            Assert.Equal("abcd", new string(buffer, 0, bufLen - 1));
        }

        [Fact]
        public void TryGetSymbolName_ZeroBuffer_ReturnsRequiredSize()
        {
            RecordingSymbolProvider provider = new()
            {
                NameResult = "Sym",
                NameLookupResult = true,
            };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            uint actual = 0;
            int hr = h.TryGetSymbolName(0x10, cchName: 0, pName: null, &actual, null);

            Assert.Equal(HResult.S_OK, hr);
            Assert.Equal(4u, actual); // length + null terminator
        }

        [Fact]
        public void TryGetSymbolName_CchNameOverflowsInt_ReturnsInvalidArg()
        {
            RecordingSymbolProvider provider = new() { NameResult = "Sym", NameLookupResult = true };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            char buffer = '\0';
            uint actual = 0;
            int hr = h.TryGetSymbolName(0x10, cchName: (uint)int.MaxValue + 1, &buffer, &actual, null);

            Assert.Equal(HResult.E_INVALIDARG, hr);
            Assert.False(provider.WasCalled, "Provider must not be called when cchName is invalid.");
        }

        [Fact]
        public void TryGetSymbolName_ProviderReturnsFalse_ReturnsEFail()
        {
            RecordingSymbolProvider provider = new() { NameLookupResult = false };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            char buffer = '\0';
            uint actual = 0;
            int hr = h.TryGetSymbolName(0x10, cchName: 1, &buffer, &actual, null);

            Assert.Equal(HResult.E_FAIL, hr);
        }

        [Fact]
        public void TryGetSymbolName_ProviderThrows_ReturnsEFail()
        {
            RecordingSymbolProvider provider = new() { ThrowOnNameLookup = true };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            char buffer = '\0';
            uint actual = 0;
            int hr = h.TryGetSymbolName(0x10, cchName: 1, &buffer, &actual, null);

            Assert.Equal(HResult.E_FAIL, hr);
        }

        [Fact]
        public void TryGetSymbolAddress_NullProvider_ReturnsNotImpl()
        {
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider: null);

            ulong address = 0;
            int hr;
            fixed (char* p = "Foo")
                hr = h.TryGetSymbolAddress((IntPtr)p, &address);

            Assert.Equal(HResult.E_NOTIMPL, hr);
            Assert.Equal(0ul, address);
        }

        [Fact]
        public void TryGetSymbolAddress_NullOutPointer_ReturnsInvalidArg()
        {
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider: null);

            int hr;
            fixed (char* p = "Foo")
                hr = h.TryGetSymbolAddress((IntPtr)p, null);

            Assert.Equal(HResult.E_INVALIDARG, hr);
        }

        [Fact]
        public void TryGetSymbolAddress_Success_WritesAddress()
        {
            RecordingSymbolProvider provider = new()
            {
                AddressLookupResult = true,
                AddressResult = 0xCAFEBABE,
            };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            ulong address = 0;
            int hr;
            fixed (char* p = "coreclr!Foo")
                hr = h.TryGetSymbolAddress((IntPtr)p, &address);

            Assert.Equal(HResult.S_OK, hr);
            Assert.Equal(0xCAFEBABEul, address);
            Assert.Equal("coreclr!Foo", provider.LastName);
        }

        [Fact]
        public void TryGetSymbolAddress_ProviderReturnsFalse_ReturnsEFailAndZeroes()
        {
            RecordingSymbolProvider provider = new() { AddressLookupResult = false };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            ulong address = 0xDEADBEEF;
            int hr;
            fixed (char* p = "Foo")
                hr = h.TryGetSymbolAddress((IntPtr)p, &address);

            Assert.Equal(HResult.E_FAIL, hr);
            Assert.Equal(0ul, address);
        }

        [Fact]
        public void TryGetSymbolAddress_EmptyName_ReturnsInvalidArg()
        {
            RecordingSymbolProvider provider = new() { AddressLookupResult = true, AddressResult = 0x1 };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            ulong address = 0;
            int hr;
            fixed (char* p = "")
                hr = h.TryGetSymbolAddress((IntPtr)p, &address);

            Assert.Equal(HResult.E_INVALIDARG, hr);
            Assert.False(provider.WasCalled);
        }

        // -- helpers --------------------------------------------------------

        private sealed class RecordingSymbolProvider : IClrSymbolProvider
        {
            public string? NameResult { get; set; }
            public ulong DisplacementResult { get; set; }
            public bool NameLookupResult { get; set; }
            public bool ThrowOnNameLookup { get; set; }

            public bool AddressLookupResult { get; set; }
            public ulong AddressResult { get; set; }

            public ulong LastAddress { get; private set; }
            public string? LastName { get; private set; }
            public bool WasCalled { get; private set; }

            public bool TryGetSymbolName(ulong address, [NotNullWhen(true)] out string? symbolName, out ulong displacement)
            {
                WasCalled = true;
                LastAddress = address;
                if (ThrowOnNameLookup)
                    throw new InvalidOperationException("boom");
                if (!NameLookupResult)
                {
                    symbolName = null;
                    displacement = 0;
                    return false;
                }
                symbolName = NameResult ?? "";
                displacement = DisplacementResult;
                return true;
            }

            public bool TryGetSymbolAddress(string symbolName, out ulong address)
            {
                WasCalled = true;
                LastName = symbolName;
                address = AddressLookupResult ? AddressResult : 0;
                return AddressLookupResult;
            }
        }

        /// <summary>
        /// Builds a real DacDataTargetCOM CCW for a synthetic DataTarget, QIs for
        /// ICLRSymbolProvider, and exposes typed wrappers around the vtable thunks.
        /// </summary>
        private sealed class SymbolProviderHarness : IDisposable
        {
            private readonly DataTarget _dataTarget;
            private readonly IntPtr _dacDataTargetPtr;
            private readonly IntPtr _symbolProviderPtr;

            private SymbolProviderHarness(DataTarget dataTarget, IntPtr dacDataTargetPtr, IntPtr symbolProviderPtr)
            {
                _dataTarget = dataTarget;
                _dacDataTargetPtr = dacDataTargetPtr;
                _symbolProviderPtr = symbolProviderPtr;
            }

            public static SymbolProviderHarness Create(IClrSymbolProvider? provider)
            {
                SyntheticDataReader reader = new(new Dictionary<ulong, byte[]>());
                DataTargetOptions options = new() { SymbolProvider = provider };
                DataTarget dt = new(reader, options);
                DacDataTarget dac = new(dt);

                IntPtr iDacDataTarget = DacDataTargetCOM.CreateIDacDataTarget(dac);
                Assert.NotEqual(IntPtr.Zero, iDacDataTarget);

                Guid iid = DacDataTarget.IID_ICLRSymbolProvider;
                int qiHr = Marshal.QueryInterface(iDacDataTarget, in iid, out IntPtr iSymbolProvider);
                Assert.Equal(HResult.S_OK, qiHr);
                Assert.NotEqual(IntPtr.Zero, iSymbolProvider);

                return new SymbolProviderHarness(dt, iDacDataTarget, iSymbolProvider);
            }

            public int TryGetSymbolName(ulong address, uint cchName, char* pName, uint* pcchNameActual, ulong* pDisplacement)
            {
                IntPtr* vtbl = *(IntPtr**)_symbolProviderPtr;
                var fn = (delegate* unmanaged<IntPtr, ulong, uint, char*, uint*, ulong*, int>)vtbl[VtblSlot_TryGetSymbolName];
                return fn(_symbolProviderPtr, address, cchName, pName, pcchNameActual, pDisplacement);
            }

            public int TryGetSymbolAddress(IntPtr namePtr, ulong* pAddress)
            {
                IntPtr* vtbl = *(IntPtr**)_symbolProviderPtr;
                var fn = (delegate* unmanaged<IntPtr, IntPtr, ulong*, int>)vtbl[VtblSlot_TryGetSymbolAddress];
                return fn(_symbolProviderPtr, namePtr, pAddress);
            }

            public void Dispose()
            {
                if (_symbolProviderPtr != IntPtr.Zero)
                    Marshal.Release(_symbolProviderPtr);
                if (_dacDataTargetPtr != IntPtr.Zero)
                    Marshal.Release(_dacDataTargetPtr);
                _dataTarget.Dispose();
            }
        }
    }
}
