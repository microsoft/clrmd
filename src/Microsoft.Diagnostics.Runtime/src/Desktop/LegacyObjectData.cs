namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct LegacyObjectData : IObjectData
    {
        private ulong _eeClass;
        private ulong _methodTable;
        private uint _objectType;
        private uint _size;
        private uint _elementType;
        private uint _dwRank;
        private uint _dwNumComponents;
        private uint _dwComponentSize;
        private ulong _arrayBoundsPtr;
        private ulong _arrayLowerBoundsPtr;

        public ClrElementType ElementType => (ClrElementType)_elementType;
        public ulong ElementTypeHandle { get; }
        public ulong RCW => 0;
        public ulong CCW => 0;
        public ulong DataPointer { get; }
    }
}