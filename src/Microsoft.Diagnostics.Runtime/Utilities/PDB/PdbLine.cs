// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    public struct PdbLine
    {
        public uint Offset { get; private set; }
        public uint LineBegin { get; private set; }
        public uint LineEnd { get; private set; }
        public ushort ColBegin { get; private set; }
        public ushort ColEnd { get; private set; }

        internal PdbLine(uint offset, uint lineBegin, ushort colBegin, uint lineEnd, ushort colEnd)
        {
            Offset = offset;
            LineBegin = lineBegin;
            ColBegin = colBegin;
            LineEnd = lineEnd;
            ColEnd = colEnd;
        }

        public override string ToString()
        {
            if (LineBegin == LineEnd)
                return string.Format("iloffs: {0} line: {1}", Offset, LineBegin);

            return string.Format("iloffs: {0} lines: {1}-{2}", Offset, LineBegin, LineEnd);
        }
    }
}
