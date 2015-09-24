// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    internal class PdbSource
    {
        //internal uint index;
        public string name;
        public Guid doctype;
        public Guid language;
        public Guid vendor;
        public Guid algorithmId;
        public byte[] checksum;
        public byte[] source;

        public PdbSource(/*uint index, */string name, Guid doctype, Guid language, Guid vendor, Guid algorithmId, byte[] checksum, byte[] source)
        {
            //this.index = index;
            this.name = name;
            this.doctype = doctype;
            this.language = language;
            this.vendor = vendor;
            this.algorithmId = algorithmId;
            this.checksum = checksum;
            this.source = source;
        }
    }
}
