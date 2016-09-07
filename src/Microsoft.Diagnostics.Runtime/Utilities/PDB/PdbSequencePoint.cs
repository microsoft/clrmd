// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    /// <summary>
    /// Represents a sequence point (multiple lines) in a source file.
    /// </summary>
    public struct PdbSequencePoint
    {
        uint _offset;
        /// <summary>
        /// The IL offset of this line.
        /// </summary>
        public uint Offset
        {
            get { return _offset; }
            private set { _offset = value; }
        }

        uint _lineBegin;
        /// <summary>
        /// The first line of this sequence point.
        /// </summary>
        public uint LineBegin
        {
            get { return _lineBegin; }
            private set { _lineBegin = value; }
        }

        uint _lineEnd;
        /// <summary>
        /// The last line of this sequence point.
        /// </summary>
        public uint LineEnd
        {
            get { return _lineEnd; }
            private set { _lineEnd = value; }
        }

        ushort _colBegin;
        /// <summary>
        /// The first column of the first line of this sequence point.
        /// </summary>
        public ushort ColBegin
        {
            get { return _colBegin; }
            private set { _colBegin = value; }
        }

        ushort _colEnd;
        /// <summary>
        /// The last column of the last line of this sequence point.
        /// </summary>
        public ushort ColEnd
        {
            get { return _colEnd; }
            private set { _colEnd = value; }
        }

        internal PdbSequencePoint(uint offset, uint lineBegin, ushort colBegin, uint lineEnd, ushort colEnd)
        {
            _offset = offset;
            _lineBegin = lineBegin;
            _colBegin = colBegin;
            _lineEnd = lineEnd;
            _colEnd = colEnd;
        }

        /// <summary>
        /// ToString override.
        /// </summary>
        public override string ToString()
        {
            if (LineBegin == LineEnd)
                return string.Format("iloffs: {0} line: {1}", Offset, LineBegin);

            return string.Format("iloffs: {0} lines: {1}-{2}", Offset, LineBegin, LineEnd);
        }
    }
}
