// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Each directory contains the 32-bit Name of the entry and an offset,
    /// relative to the beginning of the resource directory of the data associated
    /// with this directory entry.  If the name of the entry is an actual text
    /// string instead of an integer Id, then the high order bit of the name field
    /// is set to one and the low order 31-bits are an offset, relative to the
    /// beginning of the resource directory of the string, which is of type
    /// IMAGE_RESOURCE_DIRECTORY_STRING.  Otherwise the high bit is clear and the
    /// low-order 16-bits are the integer Id that identify this resource directory
    /// entry. If the directory entry is yet another resource directory (i.e. a
    /// subdirectory), then the high order bit of the offset field will be
    /// set to indicate this.  Otherwise the high bit is clear and the offset
    /// field points to a resource data entry.
    /// </summary>
    internal unsafe struct IMAGE_RESOURCE_DIRECTORY_ENTRY
    {
        public bool IsStringName => _nameOffsetAndFlag < 0;
        public int NameOffset => _nameOffsetAndFlag & 0x7FFFFFFF;

        public bool IsLeaf => (0x80000000 & _dataOffsetAndFlag) == 0;
        public int DataOffset => _dataOffsetAndFlag & 0x7FFFFFFF;
        public int Id => 0xFFFF & _nameOffsetAndFlag;

        private int _nameOffsetAndFlag;
        private int _dataOffsetAndFlag;

        internal string GetName(PEBuffer buff, int resourceStartFileOffset)
        {
            if (IsStringName)
            {
                int nameLen = *((ushort*)buff.Fetch(NameOffset + resourceStartFileOffset, 2));
                char* namePtr = (char*)buff.Fetch(NameOffset + resourceStartFileOffset + 2, nameLen);
                return new string(namePtr);
            }

            return Id.ToString();
        }

        internal static string GetTypeNameForTypeId(int typeId)
        {
            switch (typeId)
            {
                case 1:
                    return "Cursor";
                case 2:
                    return "BitMap";
                case 3:
                    return "Icon";
                case 4:
                    return "Menu";
                case 5:
                    return "Dialog";
                case 6:
                    return "String";
                case 7:
                    return "FontDir";
                case 8:
                    return "Font";
                case 9:
                    return "Accelerator";
                case 10:
                    return "RCData";
                case 11:
                    return "MessageTable";
                case 12:
                    return "GroupCursor";
                case 14:
                    return "GroupIcon";
                case 16:
                    return "Version";
                case 19:
                    return "PlugPlay";
                case 20:
                    return "Vxd";
                case 21:
                    return "Aniicursor";
                case 22:
                    return "Aniicon";
                case 23:
                    return "Html";
                case 24:
                    return "RT_MANIFEST";
            }

            return typeId.ToString();
        }
    }
}