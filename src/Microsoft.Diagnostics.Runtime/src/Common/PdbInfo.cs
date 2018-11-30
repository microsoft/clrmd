// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Information about a specific PDB instance obtained from a PE image.
    /// </summary>
    [Serializable]
    public class PdbInfo
    {
        /// <summary>
        /// The Guid of the PDB.
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// The pdb revision.
        /// </summary>
        public int Revision { get; set; }

        /// <summary>
        /// The filename of the pdb.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Creates an instance of the PdbInfo class
        /// </summary>
        public PdbInfo()
        {
        }

        /// <summary>
        /// Creates an instance of the PdbInfo class with the corresponding properties initialized
        /// </summary>
        public PdbInfo(string fileName, Guid guid, int rev)
        {
            FileName = fileName;
            Guid = guid;
            Revision = rev;
        }

        /// <summary>
        /// GetHashCode implementation.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Guid.GetHashCode() ^ Revision;
        }

        /// <summary>
        /// Override for Equals.  Returns true if the guid, age, and filenames equal.  Note that this compares only the
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>True if the objects match, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj is PdbInfo rhs)
            {
                if (Revision == rhs.Revision && Guid == rhs.Guid)
                {
                    string lhsFilename = Path.GetFileName(FileName);
                    string rhsFilename = Path.GetFileName(rhs.FileName);
                    return lhsFilename.Equals(rhsFilename, StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }

        /// <summary>
        /// To string implementation.
        /// </summary>
        /// <returns>Printing friendly version.</returns>
        public override string ToString()
        {
            return $"{Guid} {Revision} {FileName}";
        }
    }
}