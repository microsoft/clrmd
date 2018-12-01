// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    /// <summary>
    /// This class represents a constant value in source code, such as:
    /// const int Foo = 3;
    /// </summary>
    public class PdbConstant
    {
        /// <summary>
        /// The variable name of the constant.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The metadata token of this constant.
        /// </summary>
        public uint Token { get; }

        /// <summary>
        /// The value of this constant.
        /// </summary>
        public object Value { get; }

        internal PdbConstant(BitAccess bits)
        {
            uint token;
            bits.ReadUInt32(out token);
            Token = token;
            byte tag1;
            bits.ReadUInt8(out tag1);
            byte tag2;
            bits.ReadUInt8(out tag2);
            if (tag2 == 0)
            {
                Value = tag1;
            }
            else if (tag2 == 0x80)
            {
                switch (tag1)
                {
                    case 0x00: //sbyte
                        sbyte sb;
                        bits.ReadInt8(out sb);
                        Value = sb;
                        break;
                    case 0x01: //short
                        short s;
                        bits.ReadInt16(out s);
                        Value = s;
                        break;
                    case 0x02: //ushort
                        ushort us;
                        bits.ReadUInt16(out us);
                        Value = us;
                        break;
                    case 0x03: //int
                        int i;
                        bits.ReadInt32(out i);
                        Value = i;
                        break;
                    case 0x04: //uint
                        uint ui;
                        bits.ReadUInt32(out ui);
                        Value = ui;
                        break;
                    case 0x05: //float
                        Value = bits.ReadFloat();
                        break;
                    case 0x06: //double
                        Value = bits.ReadDouble();
                        break;
                    case 0x09: //long
                        long sl;
                        bits.ReadInt64(out sl);
                        Value = sl;
                        break;
                    case 0x0a: //ulong
                        ulong ul;
                        bits.ReadUInt64(out ul);
                        Value = ul;
                        break;
                    case 0x10: //string
                        string str;
                        bits.ReadBString(out str);
                        Value = str;
                        break;
                    case 0x19: //decimal
                        Value = bits.ReadDecimal();
                        break;
                }
            }

            bits.ReadCString(out string name);
            Name = name;
        }
    }
}