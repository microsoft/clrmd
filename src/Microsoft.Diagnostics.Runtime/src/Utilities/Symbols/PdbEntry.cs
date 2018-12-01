// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal struct PdbEntry : IEquatable<PdbEntry>
    {
        public string FileName;
        public Guid Guid;
        public int Revision;

        public PdbEntry(string filename, Guid guid, int revision)
        {
            FileName = filename;
            Guid = guid;
            Revision = revision;
        }

        public override int GetHashCode()
        {
            return FileName.ToLower().GetHashCode() ^ Guid.GetHashCode() ^ Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is PdbEntry && Equals((PdbEntry)obj);
        }

        public bool Equals(PdbEntry other)
        {
            return Revision == other.Revision && FileName.Equals(other.FileName, StringComparison.OrdinalIgnoreCase) && Guid == other.Guid;
        }
    }
}