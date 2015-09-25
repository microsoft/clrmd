// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    public class PdbSlot
    {
        public uint slot;
        public string name;
        public ushort flags;
        //internal uint segment;
        //internal uint address;

        internal PdbSlot(BitAccess bits, out uint typind)
        {
            AttrSlotSym slot;

            bits.ReadUInt32(out slot.index);
            bits.ReadUInt32(out slot.typind);
            bits.ReadUInt32(out slot.offCod);
            bits.ReadUInt16(out slot.segCod);
            bits.ReadUInt16(out slot.flags);
            bits.ReadCString(out slot.name);

            this.slot = slot.index;
            this.name = slot.name;
            this.flags = slot.flags;
            //this.segment = slot.segCod;
            //this.address = slot.offCod;

            typind = slot.typind;
        }
    }
}
