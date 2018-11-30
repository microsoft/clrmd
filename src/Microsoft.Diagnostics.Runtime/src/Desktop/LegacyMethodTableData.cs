using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct LegacyMethodTableData : IMethodTableData
    {
        public uint bIsFree; // everything else is NULL if this is true.
        public ulong eeClass;
        public ulong parentMethodTable;
        public ushort wNumInterfaces;
        public ushort wNumVtableSlots;
        public uint baseSize;
        public uint componentSize;
        public uint isShared; // flags & enum_flag_DomainNeutral
        public uint sizeofMethodTable;
        public uint isDynamic;
        public uint containsPointers;

        public bool ContainsPointers => containsPointers != 0;
        public uint BaseSize => baseSize;
        public uint ComponentSize => componentSize;
        public ulong EEClass => eeClass;
        public bool Free => bIsFree != 0;
        public ulong Parent => parentMethodTable;
        public bool Shared => isShared != 0;
        public uint NumMethods => wNumVtableSlots;
        public ulong ElementTypeHandle => throw new NotImplementedException();
        public uint Token => 0;
        public ulong Module => 0;
    }
}