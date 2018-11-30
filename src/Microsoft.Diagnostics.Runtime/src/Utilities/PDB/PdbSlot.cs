// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    /// <summary>
    /// The representation of a local variable slot.
    /// </summary>
    public class PdbSlot
    {
        /// <summary>
        /// The slot number.
        /// </summary>
        public uint Slot { get; }

        /// <summary>
        /// The name of this variable slot.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// the flags associated with this slot.
        /// </summary>
        public ushort Flags { get; }

        internal PdbSlot(BitAccess bits, out uint typind)
        {
            AttrSlotSym slot;

            bits.ReadUInt32(out slot.index);
            bits.ReadUInt32(out slot.typind);
            bits.ReadUInt32(out slot.offCod);
            bits.ReadUInt16(out slot.segCod);
            bits.ReadUInt16(out slot.flags);
            bits.ReadCString(out slot.name);

            Slot = slot.index;
            Name = slot.name;
            Flags = slot.flags;

            typind = slot.typind;
        }
    }
}