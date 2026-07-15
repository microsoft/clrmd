// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    /// the test builds the real CCW, QIs ICLRSymbolProvider via a
    /// <see cref="CallableCOMWrapper"/>, and invokes the vtable methods through it.
    /// </summary>
    public unsafe class ClrSymbolProviderTests
    {
        [Fact]
        public void TryGetSymbolName_NullProvider_ReturnsNotImpl()
        {
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider: null);

            char buffer = '\0';
            uint actual = 0;
            ulong displacement = 0;
            int hr = h.SymbolProvider.TryGetSymbolName(0x1000, cchName: 1, &buffer, &actual, &displacement);

            Assert.Equal(HResult.E_NOTIMPL, hr);
        }

        [Fact]
        public void TryGetSymbolName_Success_FillsBufferAndOutParams()
        {
            RecordingSymbolProvider provider = new()
            {
                NameResult = "coreclr_SymbolFoo",
                DisplacementResult = 0x42,
                NameLookupResult = true,
            };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            const int bufLen = 64;
            char* buffer = stackalloc char[bufLen];
            uint actual = 0;
            ulong displacement = 0;

            int hr = h.SymbolProvider.TryGetSymbolName(0x1234, cchName: bufLen, buffer, &actual, &displacement);

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

            int hr = h.SymbolProvider.TryGetSymbolName(0x10, cchName: bufLen, buffer, &actual, null);

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
            int hr = h.SymbolProvider.TryGetSymbolName(0x10, cchName: 0, pName: null, &actual, null);

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
            int hr = h.SymbolProvider.TryGetSymbolName(0x10, cchName: (uint)int.MaxValue + 1, &buffer, &actual, null);

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
            int hr = h.SymbolProvider.TryGetSymbolName(0x10, cchName: 1, &buffer, &actual, null);

            Assert.Equal(HResult.E_FAIL, hr);
        }

        [Fact]
        public void TryGetSymbolName_ProviderThrows_ReturnsEFail()
        {
            RecordingSymbolProvider provider = new() { ThrowOnNameLookup = true };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            char buffer = '\0';
            uint actual = 0;
            int hr = h.SymbolProvider.TryGetSymbolName(0x10, cchName: 1, &buffer, &actual, null);

            Assert.Equal(HResult.E_FAIL, hr);
        }

        [Fact]
        public void TryGetSymbolAddress_NullProvider_ReturnsNotImpl()
        {
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider: null);

            ulong address = 0;
            int hr;
            fixed (char* p = "Foo")
                hr = h.SymbolProvider.TryGetSymbolAddress(moduleBase: 0, (IntPtr)p, &address);

            Assert.Equal(HResult.E_NOTIMPL, hr);
            Assert.Equal(0ul, address);
        }

        [Fact]
        public void TryGetSymbolAddress_NullOutPointer_ReturnsInvalidArg()
        {
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider: null);

            int hr;
            fixed (char* p = "Foo")
                hr = h.SymbolProvider.TryGetSymbolAddress(moduleBase: 0, (IntPtr)p, null);

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
            fixed (char* p = "coreclr_Foo")
                hr = h.SymbolProvider.TryGetSymbolAddress(moduleBase: 0xBEEF0000, (IntPtr)p, &address);

            Assert.Equal(HResult.S_OK, hr);
            Assert.Equal(0xCAFEBABEul, address);
            Assert.Equal(0xBEEF0000ul, provider.LastModuleBase);
            Assert.Equal("coreclr_Foo", provider.LastName);
        }

        [Fact]
        public void TryGetSymbolAddress_NameContainsBang_ReturnsInvalidArg()
        {
            // '!' is reserved as the SOS module separator; the COM boundary
            // rejects qualified names so callers must split at the host
            // before reaching us.
            RecordingSymbolProvider provider = new() { AddressLookupResult = true, AddressResult = 0x1 };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            ulong address = 0;
            int hr;
            fixed (char* p = "coreclr!Foo")
                hr = h.SymbolProvider.TryGetSymbolAddress(moduleBase: 0, (IntPtr)p, &address);

            Assert.Equal(HResult.E_INVALIDARG, hr);
            Assert.False(provider.WasCalled);
        }

        [Fact]
        public void TryGetSymbolAddress_ProviderReturnsFalse_ReturnsEFailAndZeroes()
        {
            RecordingSymbolProvider provider = new() { AddressLookupResult = false };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            ulong address = 0xDEADBEEF;
            int hr;
            fixed (char* p = "Foo")
                hr = h.SymbolProvider.TryGetSymbolAddress(moduleBase: 0, (IntPtr)p, &address);

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
                hr = h.SymbolProvider.TryGetSymbolAddress(moduleBase: 0, (IntPtr)p, &address);

            Assert.Equal(HResult.E_INVALIDARG, hr);
            Assert.False(provider.WasCalled);
        }

        [Fact]
        public void TryGetFieldOffset_NullProvider_ReturnsNotImpl()
        {
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider: null);

            uint offset = 0xdead;
            int hr;
            fixed (char* t = "S_P_CoreLib_System_Exception")
            fixed (char* f = "_message")
                hr = h.SymbolProvider.TryGetFieldOffset(moduleBase: 0, (IntPtr)t, (IntPtr)f, &offset);

            Assert.Equal(HResult.E_NOTIMPL, hr);
        }

        [Fact]
        public void TryGetFieldOffset_NullOutPointer_ReturnsInvalidArg()
        {
            RecordingSymbolProvider provider = new() { FieldLookupResult = true, FieldOffsetResult = 8 };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            int hr;
            fixed (char* t = "S_P_CoreLib_System_Exception")
            fixed (char* f = "_message")
                hr = h.SymbolProvider.TryGetFieldOffset(moduleBase: 0, (IntPtr)t, (IntPtr)f, null);

            Assert.Equal(HResult.E_INVALIDARG, hr);
        }

        [Fact]
        public void TryGetFieldOffset_Success_WritesOffsetAndForwardsArgs()
        {
            RecordingSymbolProvider provider = new() { FieldLookupResult = true, FieldOffsetResult = 0x18 };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            uint offset = 0;
            int hr;
            fixed (char* t = "S_P_CoreLib_System_Exception")
            fixed (char* f = "_innerException")
                hr = h.SymbolProvider.TryGetFieldOffset(moduleBase: 0xBEEF0000, (IntPtr)t, (IntPtr)f, &offset);

            Assert.Equal(HResult.S_OK, hr);
            Assert.Equal(0x18u, offset);
            Assert.Equal(0xBEEF0000u, provider.LastModuleBase);
            Assert.Equal("S_P_CoreLib_System_Exception", provider.LastTypeName);
            Assert.Equal("_innerException", provider.LastFieldName);
        }

        [Fact]
        public void TryGetFieldOffset_ProviderReturnsFalse_ReturnsEFailAndZeroes()
        {
            RecordingSymbolProvider provider = new() { FieldLookupResult = false };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            uint offset = 0xdead;
            int hr;
            fixed (char* t = "S_P_CoreLib_System_Exception")
            fixed (char* f = "_missing")
                hr = h.SymbolProvider.TryGetFieldOffset(moduleBase: 0, (IntPtr)t, (IntPtr)f, &offset);

            Assert.Equal(HResult.E_FAIL, hr);
            Assert.Equal(0u, offset);
        }

        [Fact]
        public void TryGetFieldOffset_EmptyTypeName_ReturnsInvalidArg()
        {
            RecordingSymbolProvider provider = new() { FieldLookupResult = true, FieldOffsetResult = 8 };
            using SymbolProviderHarness h = SymbolProviderHarness.Create(provider);

            uint offset = 0;
            int hr;
            fixed (char* t = "")
            fixed (char* f = "_message")
                hr = h.SymbolProvider.TryGetFieldOffset(moduleBase: 0, (IntPtr)t, (IntPtr)f, &offset);

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

            public bool FieldLookupResult { get; set; }
            public uint FieldOffsetResult { get; set; }

            public ulong LastModuleBase { get; private set; }
            public ulong LastAddress { get; private set; }
            public string? LastName { get; private set; }
            public string? LastTypeName { get; private set; }
            public string? LastFieldName { get; private set; }
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

            public bool TryGetSymbolAddress(ulong moduleBase, string symbolName, out ulong address)
            {
                WasCalled = true;
                LastModuleBase = moduleBase;
                LastName = symbolName;
                address = AddressLookupResult ? AddressResult : 0;
                return AddressLookupResult;
            }

            public bool TryGetFieldOffset(ulong moduleBase, string typeName, string fieldName, out uint offset)
            {
                WasCalled = true;
                LastModuleBase = moduleBase;
                LastTypeName = typeName;
                LastFieldName = fieldName;
                offset = FieldLookupResult ? FieldOffsetResult : 0;
                return FieldLookupResult;
            }
        }

        /// <summary>
        /// <see cref="CallableCOMWrapper"/> over the ICLRSymbolProvider that
        /// <see cref="DacDataTargetCOM"/> exposes. Mirrors the established
        /// pattern used by <c>SOSDac6</c>, <c>SOSDac12</c>, etc.
        /// </summary>
        private sealed class ClrSymbolProviderWrapper : CallableCOMWrapper
        {
            public ClrSymbolProviderWrapper(IntPtr pUnknown)
                : base(in DacDataTarget.IID_ICLRSymbolProvider, pUnknown)
            {
            }

            private ref readonly Vtable VTable => ref Unsafe.AsRef<Vtable>(_vtable);

            public int TryGetSymbolName(ulong address, uint cchName, char* pName, uint* pcchActual, ulong* pDisplacement)
                => VTable.TryGetSymbolName(Self, address, cchName, pName, pcchActual, pDisplacement);

            public int TryGetSymbolAddress(ulong moduleBase, IntPtr name, ulong* pAddress)
                => VTable.TryGetSymbolAddress(Self, moduleBase, name, pAddress);

            public int TryGetFieldOffset(ulong moduleBase, IntPtr typeName, IntPtr fieldName, uint* pOffset)
                => VTable.TryGetFieldOffset(Self, moduleBase, typeName, fieldName, pOffset);

            [StructLayout(LayoutKind.Sequential)]
            private readonly struct Vtable
            {
                public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong, uint, char*, uint*, ulong*, int> TryGetSymbolName;
                public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong, IntPtr, ulong*, int> TryGetSymbolAddress;
                public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong, IntPtr, IntPtr, uint*, int> TryGetFieldOffset;
            }
        }

        /// <summary>
        /// Builds a real <see cref="DacDataTargetCOM"/> CCW around a synthetic
        /// <see cref="DataTarget"/>, then exposes the ICLRSymbolProvider through
        /// a <see cref="CallableCOMWrapper"/> wrapper.
        /// </summary>
        private sealed class SymbolProviderHarness : IDisposable
        {
            private readonly DataTarget _dataTarget;

            public ClrSymbolProviderWrapper SymbolProvider { get; }

            private SymbolProviderHarness(DataTarget dataTarget, ClrSymbolProviderWrapper symbolProvider)
            {
                _dataTarget = dataTarget;
                SymbolProvider = symbolProvider;
            }

            public static SymbolProviderHarness Create(IClrSymbolProvider? provider)
            {
                SyntheticDataReader reader = new(new Dictionary<ulong, byte[]>());
                DataTargetOptions options = new() { SymbolProvider = provider };
                DataTarget dt = new(reader, options);
                DacDataTarget dac = new(dt);

                IntPtr iDacDataTarget = DacDataTargetCOM.CreateIDacDataTarget(dac);
                Assert.NotEqual(IntPtr.Zero, iDacDataTarget);

                // CallableCOMWrapper QIs for ICLRSymbolProvider and Releases pUnknown internally.
                ClrSymbolProviderWrapper wrapper = new(iDacDataTarget);
                return new SymbolProviderHarness(dt, wrapper);
            }

            public void Dispose()
            {
                SymbolProvider.Dispose();
                _dataTarget.Dispose();
            }
        }
    }
}
