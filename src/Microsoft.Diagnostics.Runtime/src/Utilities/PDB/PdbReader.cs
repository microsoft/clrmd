// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using SharpPdb.Managed;
using System;

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    /// <summary>
    /// An object that can map offsets in an IL stream to source locations and block scopes.
    /// </summary>
    public static class PdbReader
    {
        /// <summary>
        /// Gets the properties of a given pdb.  Throws IOException on error.
        /// </summary>
        /// <param name="pdbFile">The pdb file to load.</param>
        /// <param name="signature">The signature of pdbFile.</param>
        /// <param name="age">The age of pdbFile.</param>
        public static void GetPdbProperties(string pdbFile, out Guid signature, out int age)
        {
            using (IPdbFile pdb = OpenPdb(pdbFile))
            {
                signature = pdb.Guid;
                age = pdb.Age;
            }
        }

        /// <summary>
        /// Constructs a IPdbFile from a path on disk.
        /// </summary>
        /// <param name="fileName">The pdb on disk to load.</param>
        public static IPdbFile OpenPdb(string fileName)
        {
            return PdbFileReader.OpenPdb(fileName);
        }
    }
}
