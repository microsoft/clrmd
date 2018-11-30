// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    /// <summary>
    /// A source file in the program.
    /// </summary>
    public class PdbSource
    {
        /// <summary>
        /// The name of the source file.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The DocType for this source.
        /// </summary>
        public Guid DocType { get; }

        /// <summary>
        /// Pdb source language.
        /// </summary>
        public Guid Language { get; }

        /// <summary>
        /// Pdb source vendor
        /// </summary>
        public Guid Vendor { get; }

        /// <summary>
        /// Pdb algorithm id.
        /// </summary>
        public Guid AlgorithmId { get; }

        /// <summary>
        /// Checksum for this pdb.
        /// </summary>
        public byte[] Checksum { get; }

        /// <summary>
        /// The embeded source in this pdb.
        /// </summary>
        public byte[] Source { get; }

        internal PdbSource(string name, Guid doctype, Guid language, Guid vendor, Guid algorithmId, byte[] checksum, byte[] source)
        {
            Name = name;
            DocType = doctype;
            Language = language;
            Vendor = vendor;
            AlgorithmId = algorithmId;
            Checksum = checksum;
            Source = source;
        }
    }
}