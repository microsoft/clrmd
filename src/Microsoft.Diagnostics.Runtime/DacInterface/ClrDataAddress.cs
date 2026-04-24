// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
