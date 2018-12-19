namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A wrapper over the IMAGE_SECTION_HEADER structure.
    /// https://msdn.microsoft.com/en-us/library/windows/desktop/ms680341(v=vs.85).aspx
    /// </summary>
    public class SectionHeader
    {
        public string Name { get; private set; }
        public uint VirtualSize { get; private set; }
        public uint VirtualAddress { get; private set; }
        public uint SizeOfRawData { get; private set; }
        public uint PointerToRawData { get; private set; }
        public uint PointerToRelocations { get; private set; }
        public uint PointerToLineNumbers { get; private set; }
        public ushort NumberOfRelocations { get; private set; }
        public ushort NumberOfLineNumbers { get; private set; }
        public IMAGE_SCN Characteristics { get; private set; }

        internal SectionHeader(IMAGE_SECTION_HEADER section)
        {
            Name = section.Name;
            VirtualSize = section.VirtualSize;
            VirtualAddress = section.VirtualAddress;
            SizeOfRawData = section.SizeOfRawData;
            PointerToRawData = section.PointerToRawData;
            PointerToRelocations = section.PointerToRelocations;
            PointerToLineNumbers = section.PointerToLinenumbers;
            NumberOfRelocations = section.NumberOfRelocations;
            NumberOfLineNumbers = section.NumberOfLinenumbers;
            Characteristics = (IMAGE_SCN)section.Characteristics;
        }

        public override string ToString() => Name;
    }
}
