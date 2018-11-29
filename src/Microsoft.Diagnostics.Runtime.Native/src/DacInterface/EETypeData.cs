namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public readonly struct EETypeData
    {
        public readonly uint ObjectType; // everything else is NULL if this is true.
        public readonly ulong CanonicalMethodTable;
        public readonly ulong ParentMethodTable;
        public readonly ushort NumInterfaces;
        public readonly ushort NumVTableSlots;
        public readonly uint BaseSize;
        public readonly uint ComponentSize;
        public readonly uint SizeOfMethodTable;
        public readonly uint ContainsPointers;
        public readonly ulong ElementTypeHandle;
    }
}