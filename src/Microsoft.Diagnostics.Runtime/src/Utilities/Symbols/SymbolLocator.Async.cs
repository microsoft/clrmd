// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    public abstract partial class SymbolLocator
    {
        /// <summary>
        /// Attempts to locate a binary via the symbol server.  This function will then copy the file
        /// locally to the symbol cache and return the location of the local file on disk.
        /// </summary>
        /// <param name="fileName">The filename that the binary is indexed under.</param>
        /// <param name="buildTimeStamp">The build timestamp the binary is indexed under.</param>
        /// <param name="imageSize">The image size the binary is indexed under.</param>
        /// <param name="checkProperties">Whether or not to validate the properties of the binary after download.</param>
        /// <returns>A full path on disk (local) of where the binary was copied to, null if it was not found.</returns>
        public async Task<string> FindBinaryAsync(string fileName, uint buildTimeStamp, uint imageSize, bool checkProperties = true)
        {
            return await FindBinaryAsync(fileName, (int)buildTimeStamp, (int)imageSize, checkProperties);
        }

        /// <summary>
        /// Attempts to locate a binary via the symbol server.  This function will then copy the file
        /// locally to the symbol cache and return the location of the local file on disk.
        /// </summary>
        /// <param name="fileName">The filename that the binary is indexed under.</param>
        /// <param name="buildTimeStamp">The build timestamp the binary is indexed under.</param>
        /// <param name="imageSize">The image size the binary is indexed under.</param>
        /// <param name="checkProperties">Whether or not to validate the properties of the binary after download.</param>
        /// <returns>A full path on disk (local) of where the binary was copied to, null if it was not found.</returns>
        public abstract Task<string> FindBinaryAsync(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true);

        /// <summary>
        /// Attempts to locate a binary via the symbol server.  This function will then copy the file
        /// locally to the symbol cache and return the location of the local file on disk.
        /// </summary>
        /// <param name="module">The module to locate.</param>
        /// <param name="checkProperties">Whether or not to validate the properties of the binary after download.</param>
        /// <returns>A full path on disk (local) of where the binary was copied to, null if it was not found.</returns>
        public async Task<string> FindBinaryAsync(ModuleInfo module, bool checkProperties = true)
        {
            return await FindBinaryAsync(module.FileName, module.TimeStamp, module.FileSize, checkProperties);
        }

        /// <summary>
        /// Attempts to locate a dac via the symbol server.  This function will then copy the file
        /// locally to the symbol cache and return the location of the local file on disk.  Note that
        /// the dac should not validate if the properties of the file match the one it was indexed under.
        /// </summary>
        /// <param name="dac">The dac to locate.</param>
        /// <returns>A full path on disk (local) of where the binary was copied to, null if it was not found.</returns>
        public async Task<string> FindBinaryAsync(DacInfo dac)
        {
            return await FindBinaryAsync(dac, false);
        }

        /// <summary>
        /// Attempts to locate the pdb for a given module.
        /// </summary>
        /// <param name="module">The module to locate the pdb for.</param>
        /// <returns>A full path on disk (local) of where the pdb was copied to.</returns>
        public async Task<string> FindPdbAsync(ModuleInfo module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            PdbInfo pdb = module.Pdb;
            if (pdb == null)
                return null;

            return await FindPdbAsync(pdb);
        }

        /// <summary>
        /// Attempts to locate the pdb for a given module.
        /// </summary>
        /// <param name="pdb">The pdb to locate.</param>
        /// <returns>A full path on disk (local) of where the pdb was copied to.</returns>
        public async Task<string> FindPdbAsync(PdbInfo pdb)
        {
            if (pdb == null)
                throw new ArgumentNullException(nameof(pdb));

            return await FindPdbAsync(pdb.FileName, pdb.Guid, pdb.Revision);
        }

        /// <summary>
        /// Attempts to locate a pdb based on its name, guid, and revision number.
        /// </summary>
        /// <param name="pdbName">The name the pdb is indexed under.</param>
        /// <param name="pdbIndexGuid">The guid the pdb is indexed under.</param>
        /// <param name="pdbIndexAge">The age of the pdb.</param>
        /// <returns>A full path on disk (local) of where the pdb was copied to.</returns>
        public abstract Task<string> FindPdbAsync(string pdbName, Guid pdbIndexGuid, int pdbIndexAge);

        /// <summary>
        /// Copies the given file from the input stream into fullDestPath.
        /// </summary>
        /// <param name="input">The input stream to copy the file from.</param>
        /// <param name="fullSrcPath">The source of this file.  This is for informational/logging purposes and shouldn't be opened directly.</param>
        /// <param name="fullDestPath">The destination path of where the file should go on disk.</param>
        /// <param name="size">The length of the given file.  (Also for informational purposes, do not use this as part of a copy loop.</param>
        /// <returns>A task indicating when the copy is completed.</returns>
        protected abstract Task CopyStreamToFileAsync(Stream input, string fullSrcPath, string fullDestPath, long size);
    }
}