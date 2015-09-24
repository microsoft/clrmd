// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    internal struct PdbLine
    {
        public uint offset;
        public uint lineBegin;
        public uint lineEnd;
        public ushort colBegin;
        public ushort colEnd;

        internal PdbLine(uint offset, uint lineBegin, ushort colBegin, uint lineEnd, ushort colEnd)
        {
            this.offset = offset;
            this.lineBegin = lineBegin;
            this.colBegin = colBegin;
            this.lineEnd = lineEnd;
            this.colEnd = colEnd;
        }
    }
}
