// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    public class PdbLines
    {
        public PdbSource file;
        public PdbLine[] lines;

        internal PdbLines(PdbSource file, uint count)
        {
            this.file = file;
            this.lines = new PdbLine[count];
        }
    }
}
