// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
                throw new ArgumentNullException("module");

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
                throw new ArgumentNullException("pdb");

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
    }


    /// <summary>
    /// In v4.5, this class supports multithreading.
    /// </summary>
    public partial class DefaultSymbolLocator : SymbolLocator
    {
        static Dictionary<PdbEntry, Task<string>> s_pdbs = new Dictionary<PdbEntry, Task<string>>();
        static Dictionary<FileEntry, Task<string>> s_files = new Dictionary<FileEntry, Task<string>>();
        static Dictionary<string, Task<bool>> s_copy = new Dictionary<string, Task<bool>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Attempts to locate a binary via the symbol server.  This function will then copy the file
        /// locally to the symbol cache and return the location of the local file on disk.
        /// </summary>
        /// <param name="fileName">The filename that the binary is indexed under.</param>
        /// <param name="buildTimeStamp">The build timestamp the binary is indexed under.</param>
        /// <param name="imageSize">The image size the binary is indexed under.</param>
        /// <param name="checkProperties">Whether or not to validate the properties of the binary after download.</param>
        /// <returns>A full path on disk (local) of where the binary was copied to, null if it was not found.</returns>
        public override async Task<string> FindBinaryAsync(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            string simpleFilename = Path.GetFileName(fileName);
            FileEntry fileEntry = new FileEntry(simpleFilename, buildTimeStamp, imageSize);

            Task<string> result = null;
            lock (s_files)
            {
                if (!s_files.TryGetValue(fileEntry, out result))
                    result = s_files[fileEntry] = DownloadFileWorker(fileName, simpleFilename, buildTimeStamp, imageSize, checkProperties);
            }

            return await result;
        }

        /// <summary>
        /// Attempts to locate a pdb based on its name, guid, and revision number.
        /// </summary>
        /// <param name="pdbName">The name the pdb is indexed under.</param>
        /// <param name="pdbIndexGuid">The guid the pdb is indexed under.</param>
        /// <param name="pdbIndexAge">The age of the pdb.</param>
        /// <returns>A full path on disk (local) of where the pdb was copied to.</returns>
        public async override Task<string> FindPdbAsync(string pdbName, Guid pdbIndexGuid, int pdbIndexAge)
        {
            if (string.IsNullOrWhiteSpace(pdbName))
                throw new ArgumentNullException(nameof(pdbName));

            // Ensure we don't attempt to download the same pdb multiple times.
            string pdbSimpleName = Path.GetFileName(pdbName);
            PdbEntry pdbEntry = new PdbEntry(pdbSimpleName, pdbIndexGuid, pdbIndexAge);

            Task<string> result = null;
            lock (s_pdbs)
            {
                if (!s_pdbs.TryGetValue(pdbEntry, out result))
                    result = s_pdbs[pdbEntry] = DownloadPdbWorker(pdbName, pdbSimpleName, pdbIndexGuid, pdbIndexAge);
            }

            return await result;
        }

        private async Task<string> DownloadPdbWorker(string pdbFullPath, string pdbSimpleName, Guid pdbIndexGuid, int pdbIndexAge)
        {
            string pdbIndexPath = GetIndexPath(pdbSimpleName, pdbIndexGuid, pdbIndexAge);
            string fullDestPath = Path.Combine(SymbolCache, pdbIndexPath);

            Func<string, bool> match = (file) => ValidatePdb(file, pdbIndexGuid, pdbIndexAge);
            string result = CheckLocalPaths(pdbFullPath, pdbSimpleName, fullDestPath, match);
            if (result != null)
                return result;
            
            result = await SearchSymbolServerForFile(pdbSimpleName, pdbIndexPath, fullDestPath, match);
            return result;
        }

        private async Task<string> DownloadFileWorker(string fileFullPath, string fileSimpleName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            string fileIndexPath = GetIndexPath(fileSimpleName, buildTimeStamp, imageSize);
            string fullDestPath = Path.Combine(SymbolCache, fileIndexPath);

            Func<string, bool> match = (file) => ValidateBinary(file, buildTimeStamp, imageSize, checkProperties);
            string result = CheckLocalPaths(fileFullPath, fileSimpleName, fullDestPath, match);
            if (result != null)
                return result;
            
            result = await SearchSymbolServerForFile(fileFullPath, fileIndexPath, fullDestPath, match);
            return result;
        }

        private Task<string> SearchSymbolServerForFile(string fileFullPath, string fileIndexPath, string fullDestPath, Func<string, bool> match)
        {
            throw new NotImplementedException();
        }

        private string CheckLocalPaths(string fullName, string simpleName, string fullDestPath, Func<string, bool> matches)
        {
            // We were given a full path instead of simply "foo.bar".
            if (fullName != simpleName)
            {
                if (matches(fullName))
                    return fullName;
            }

            // Check the target path too.
            if (File.Exists(fullDestPath))
            {
                if (matches(fullDestPath))
                    return fullDestPath;

                // We somehow got a bad file here...this shouldn't have happened.
                File.Delete(fullDestPath);
            }

            return null;
        }

        /*
        protected override bool CopyStreamToFile(Stream stream, string fullSrcPath, string fullDestPath, long size)
        {
            return CopyStreamToFileAsync(stream, fullSrcPath, fullDestPath, size).Result;
        }


        /// <summary>
        /// Copies a given stream to a file.
        /// </summary>
        /// <param name="fullSrcPath">The original source location of "stream".  This may be a URL or null.</param>
        /// <param name="fullDestPath">The full destination path to copy the file to.</param>
        /// <param name="size">A hint as to the length of the stream.  This may be 0 or negative if the length is unknown.</param>
        /// <returns>True if the method successfully copied the file, false otherwise.</returns>
        protected async Task<bool> CopyStreamToFileAsync(string fullSrcPath, string fullDestPath, long size)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath));

            Task result = null;
            lock (s_copy)
                s_copy.TryGetValue(fullDestPath, out result);

            if (result != null)
            {
                return await result;
            }

            using (FileStream input = File.OpenRead(fullSrcPath))
                await CopyAsync(input, fullDestPath);
        }

        private static async Task<bool> CopyStreamToFileAsync(Stream input, string fullSrcPath, string fullDestPath, long size)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath));

            Task result;
            FileStream output = null;
            try
            {
                lock (s_copy)
                {
                    if (!s_copy.TryGetValue(fullDestPath, out result))
                    {
                        if (File.Exists(fullDestPath))
                            return true;

                        output = new FileStream(fullDestPath, FileMode.CreateNew);
                        s_copy[fullDestPath] = result = input.CopyToAsync(output);
                    }
                }

                await result;
            }
            finally
            {
                if (output != null)
                    output.Dispose();
            }
        }
        */

        private string GetFileEntry(FileEntry entry)
        {
            throw new NotImplementedException();
        }

        private void SetFileEntry(FileEntry entry, string value)
        {
            throw new NotImplementedException();
        }


        private string GetPdbEntry(PdbEntry entry)
        {
            throw new NotImplementedException();
        }

        private void SetPdbEntry(PdbEntry entry, string value)
        {
            throw new NotImplementedException();
        }
    }
}