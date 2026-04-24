// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// ===== ClrDataAddress: Sign Extension and Marshalling Design =====
//
// The CLR DAC uses CLRDATA_ADDRESS (typedef ULONG64) for target-process addresses
// on the COM wire. On 32-bit targets, the legacy DAC sign-extends addresses with
// bit 31 set. For example:
//
//   32-bit address:       0x80123456
//   Wire (CLRDATA_ADDRESS): 0xFFFFFFFF_80123456
//
// This convention exists for compatibility with WinDbg and OS debugging APIs.
// The sign extension must be stripped to recover the actual 32-bit address, and
// re-applied when passing addresses back to the DAC.
//
// ---- Runtime (C++) conversion macros (dacimpl.h) ----
//
//   TO_CDADDR(taddr):   (CLRDATA_ADDRESS)(LONG_PTR)(taddr)     // sign-extend
//   CLRDATA_ADDRESS_TO_TADDR(cda):  validates range, then (TADDR)cda  // truncate
//
// ---- ClrMD (C#) equivalents ----
//
//   FromAddress(addr, target):  (ulong)(long)(int)(uint)addr    // sign-extend
//   ToAddress(target):          (ulong)(uint)_value             // strip sign-ext
//
// The cast chains mirror the C++ macros exactly:
//   - FromAddress: uint→int (signed reinterpret) →long (sign-extend) →ulong
//   - ToAddress:   uint (truncate to 32 bits) →ulong (zero-extend)
//
// ---- ABI boundary: why we marshal as ulong, not ClrDataAddress ----
//
// ClrDataAddress is a readonly struct wrapping a single ulong. On the wire, the
// DAC's COM vtable declares CLRDATA_ADDRESS params as ULONG64 — a primitive.
// Different platform ABIs classify single-field structs differently for register
// passing. Since ClrMD targets netstandard2.0 (no source-generated COM), we can't
// guarantee a C# struct will be passed identically to a bare ulong.
//
// Solution: every vtable slot carrying a CLRDATA_ADDRESS is typed as plain ulong
// (annotated /*ClrDataAddress*/). Managed wrappers accept ClrDataAddress and call
// .ToInteropAddress() (a no-op unwrap) before passing to the vtable. Pointer forms
// (ref/out/ClrDataAddress*) are safe as-is — the ABI sees them as plain pointers.
//
// ---- Data flow ----
//
//   DacImplementation (ulong)
//       │ FromAddress(addr, _target)
//       ▼
//   SosDac wrapper (ClrDataAddress)
//       │ .ToInteropAddress()
//       ▼
//   Vtable call (ulong on the wire)
//       │
//   Native DAC processes request
//       │
//   Returns via out struct fields (ClrDataAddress, binary-compatible with ULONG64)
//       │ .ToAddress(_target)
//       ▼
//   DacImplementation (ulong — clean target address)
//       │
//   Public API (ulong — ClrObject.Address, ClrType.MethodTable, etc.)
//
// ClrDataAddress is confined to DacInterface/ and DacImplementation/. It never
// leaks into AbstractDac/, Implementation/, or the public API surface.
//

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// A managed-side wrapper for CLR's CLRDATA_ADDRESS (a signed 64-bit integer on the wire).
    /// <para>
    /// On 32-bit targets the legacy DAC sign-extends addresses with bit 31 set; cDAC does not.
    /// ClrDataAddress is opaque; conversions to/from target addresses must go through the
    /// static helpers on this type, which take a <see cref="TargetProperties"/> describing the
    /// DAC flavor and pointer size being used.
    /// </para>
    /// <para>
    /// <b>ABI boundary policy.</b> <see cref="ClrDataAddress"/> is managed-only: it never
    /// crosses an unmanaged calling-convention boundary by value. Every vtable slot (and every
    /// unmanaged callback) that semantically carries a CLRDATA_ADDRESS is typed as
    /// <see cref="ulong"/> (annotated with a <c>/*ClrDataAddress*/</c> comment), and managed
    /// wrappers unwrap via <see cref="ToInteropAddress"/> / <see cref="FromInteropAddress"/>.
    /// This removes any cross-platform ambiguity in how a one-field struct is classified for
    /// register/stack passing versus a primitive <see cref="ulong"/>. Pointer forms
    /// (<c>ref</c>/<c>out</c>/<c>ClrDataAddress*</c>) are safe as-is because the ABI sees them
    /// as plain pointers regardless of pointee type; those forms are preserved in signatures
    /// so DAC-produced buffers can be consumed without an extra copy.
    /// </para>
    /// </summary>
    [DebuggerDisplay("{_value,h}")]
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ClrDataAddress : IEquatable<ClrDataAddress>
    {
        private readonly ulong _value;

        /// <summary>
        /// Creates an instance of ClrDataAddress that wraps a raw wire value.
        /// Most callers should build <see cref="ClrDataAddress"/> values through
        /// <see cref="FromAddress(ulong, TargetProperties)"/> instead of this constructor.
        /// </summary>
        internal ClrDataAddress(ulong rawValue) => _value = rawValue;

        /// <summary>Returns true if this address is zero/null.</summary>
        public bool IsNull => _value == 0;

        /// <summary>A null (zero) ClrDataAddress.</summary>
        public static ClrDataAddress Null => default;

        public bool Equals(ClrDataAddress other) => _value == other._value;
        public override bool Equals(object? obj) => obj is ClrDataAddress other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();

        public static bool operator ==(ClrDataAddress left, ClrDataAddress right) => left._value == right._value;
        public static bool operator !=(ClrDataAddress left, ClrDataAddress right) => left._value != right._value;

        public override string ToString() => $"0x{_value:x}";

        /// <summary>
        /// Converts this <see cref="ClrDataAddress"/> returned from the DAC to a target-process
        /// pointer, un-sign-extending the high 32 bits on 32-bit targets.
        /// </summary>
        public ulong ToAddress(TargetProperties target) =>
            target.PointerSize == 4 ? (ulong)(uint)_value : _value;

        /// <summary>
        /// Creates a <see cref="ClrDataAddress"/> for passing into the DAC. On 32-bit targets
        /// the legacy DAC expects addresses to be sign-extended (CLRDATA_ADDRESS_TO_TADDR
        /// validates that signed(addr) is in [INT_MIN, INT_MAX]).
        /// </summary>
        public static ClrDataAddress FromAddress(ulong address, TargetProperties target)
        {
            if (target.PointerSize == 4)
                return new ClrDataAddress(unchecked((ulong)(long)(int)(uint)address));

            return new ClrDataAddress(address);
        }

        /// <summary>
        /// Unwraps this <see cref="ClrDataAddress"/> to the raw <see cref="ulong"/> wire value
        /// expected by the COM vtable (where signatures are typed as ulong for ABI safety).
        /// Does not apply any sign-extension transformation — the wrapper value is passed
        /// through unchanged.
        /// </summary>
        public ulong ToInteropAddress() => _value;

        /// <summary>
        /// Wraps a raw <see cref="ulong"/> received from a COM callback
        /// (e.g. <see cref="DacDataTarget.ReadVirtual"/>) as a <see cref="ClrDataAddress"/>
        /// with no transformation.
        /// </summary>
        public static ClrDataAddress FromInteropAddress(ulong rawValue) => new ClrDataAddress(rawValue);
    }
}
