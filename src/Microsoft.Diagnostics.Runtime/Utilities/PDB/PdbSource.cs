// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    public class PdbSource
    {
        public string Name { get; private set; }
        public Guid DocType { get; private set; }
        public Guid Language { get; private set; }
        public Guid Vendor { get; private set; }
        public Guid AlgorithmId { get; private set; }
        public byte[] Checksum { get; private set; }
        public byte[] Source { get; private set; }

        public PdbSource(string name, Guid doctype, Guid language, Guid vendor, Guid algorithmId, byte[] checksum, byte[] source)
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
