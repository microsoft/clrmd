// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// A representation of CLR's CLRDATA_ADDRESS, which is a signed 64bit integer.
    /// Unfortuantely this can cause issues when inspecting 32bit processes, since
    /// if the highest bit is set the value will be sign-extended.  This struct is
    /// meant to
    /// </summary>
    [DebuggerDisplay("{AsUInt64()}")]
    public readonly struct ClrDataAddress
    {
        /// <summary>
        /// Gets raw value of this address.  May be sign-extended if inspecting a 32bit process.
        /// </summary>
        public long Value { get; }

        /// <summary>
        /// Creates an instance of ClrDataAddress.
        /// </summary>
        /// <param name="value"></param>
        public ClrDataAddress(long value) => Value = value;

        /// <summary>
        /// Returns the value of this address and un-sign extends the value if appropriate.
        /// </summary>
        /// <param name="cda">The address to convert.</param>
        public static implicit operator ulong(ClrDataAddress cda) => cda.AsUInt64();

        public static implicit operator ClrDataAddress(ulong value) => new ClrDataAddress(IntPtr.Size == 4 ? unchecked((int)value) : unchecked((long)value));

        /// <summary>
        /// Returns the value of this address and un-sign extends the value if appropriate.
        /// </summary>
        /// <returns>The value of this address and un-sign extends the value if appropriate.</returns>
        private ulong AsUInt64() => IntPtr.Size == 4 ? unchecked((uint)Value) : unchecked((ulong)Value);
    }
}
