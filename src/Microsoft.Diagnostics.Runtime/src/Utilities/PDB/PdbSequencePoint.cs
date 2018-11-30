// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    /// <summary>
    /// Represents a sequence point (multiple lines) in a source file.
    /// </summary>
    public struct PdbSequencePoint
    {
        /// <summary>
        /// The IL offset of this line.
        /// </summary>
        public uint Offset { get; }

        /// <summary>
        /// The first line of this sequence point.
        /// </summary>
        public uint LineBegin { get; }

        /// <summary>
        /// The last line of this sequence point.
        /// </summary>
        public uint LineEnd { get; }

        /// <summary>
        /// The first column of the first line of this sequence point.
        /// </summary>
        public ushort ColBegin { get; }

        /// <summary>
        /// The last column of the last line of this sequence point.
        /// </summary>
        public ushort ColEnd { get; }

        internal PdbSequencePoint(uint offset, uint lineBegin, ushort colBegin, uint lineEnd, ushort colEnd)
        {
            Offset = offset;
            LineBegin = lineBegin;
            ColBegin = colBegin;
            LineEnd = lineEnd;
            ColEnd = colEnd;
        }

        /// <summary>
        /// ToString override.
        /// </summary>
        public override string ToString()
        {
            if (LineBegin == LineEnd)
                return $"iloffs: {Offset} line: {LineBegin}";

            return $"iloffs: {Offset} lines: {LineBegin}-{LineEnd}";
        }
    }
}