// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// This class is a general purpose symbol locator and binary locator.
    /// </summary>
    public abstract partial class SymbolLocator
    {
        /// <summary>
        /// The raw symbol path.  You should probably use the SymbolPath property instead.
        /// </summary>
        protected volatile string _symbolPath;
        /// <summary>
        /// The raw symbol cache.  You should probably use the SymbolCache property instead.
        /// </summary>
        protected volatile string _symbolCache;

        /// <summary>
        /// The timeout (in milliseconds) used when contacting each individual server.  This is not a total timeout for the entire
        /// symbol server operation.
        /// </summary>
        public int Timeout { get; set; } = 60000;

        /// <summary>
        /// A set of pdbs that we did not find when requested.  This set is SymbolLocator specific (not global
        /// like successful downloads) and is cleared when we change the symbol path or cache.
        /// </summary>
        internal volatile HashSet<PdbEntry> _missingPdbs = new HashSet<PdbEntry>();

        /// <summary>
        /// A set of files that we did not find when requested.  This set is SymbolLocator specific (not global
        /// like successful downloads) and is cleared when we change the symbol path or cache.
        /// </summary>
        internal volatile HashSet<FileEntry> _missingFiles = new HashSet<FileEntry>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public SymbolLocator()
        {
            string sympath = _NT_SYMBOL_PATH;
            if (string.IsNullOrEmpty(sympath))
                sympath = MicrosoftSymbolServerPath;

            SymbolPath = sympath;
        }

        /// <summary>
        /// Return the string representing a symbol path for the 'standard' microsoft symbol servers.
        /// This returns the public msdl.microsoft.com server if outside Microsoft.
        /// </summary>
        public static string MicrosoftSymbolServerPath
        {
            get
            {
                bool first = true;
                StringBuilder result = new StringBuilder();

                foreach (string path in MicrosoftSymbolServers)
                {
                    if (!first)
                        result.Append(';');

                    result.Append("SRV*");
                    result.Append(path);
                    first = false;
                }

                return result.ToString();
            }
        }

        /// <summary>
        /// Retrieves a list of the default Microsoft symbol servers.
        /// </summary>
        public static string[] MicrosoftSymbolServers { get; } = {"http://msdl.microsoft.com/download/symbols", "http://referencesource.microsoft.com/symbols"};

        /// <summary>
        /// This property gets and sets the global _NT_SYMBOL_PATH environment variable.
        /// This is the global setting for symbol paths on a computer.
        /// </summary>
        public static string _NT_SYMBOL_PATH
        {
            get
            {
                string ret = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
                return ret ?? "";
            }
            set => Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", value);
        }

        /// <summary>
        /// Gets or sets the local symbol file cache.  This is the location that
        /// all symbol files are downloaded to on your computer.
        /// </summary>
        public string SymbolCache
        {
            get
            {
                string cache = _symbolCache;
                if (!string.IsNullOrEmpty(cache))
                    return cache;

                string tmp = Path.GetTempPath();
                if (string.IsNullOrEmpty(tmp))
                    tmp = ".";

                return Path.Combine(tmp, "symbols");
            }
            set
            {
                _symbolCache = value;
                if (!string.IsNullOrEmpty(value))
                    Directory.CreateDirectory(value);

                SymbolPathOrCacheChanged();
            }
        }

        /// <summary>
        /// Gets or sets the SymbolPath this object uses to attempt to find PDBs and binaries.
        /// </summary>
        public string SymbolPath
        {
            get => _symbolPath ?? "";

            set
            {
                _symbolPath = (value ?? "").Trim();

                SymbolPathOrCacheChanged();
            }
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
        public string FindBinary(string fileName, uint buildTimeStamp, uint imageSize, bool checkProperties = true)
        {
            return FindBinary(fileName, (int)buildTimeStamp, (int)imageSize, checkProperties);
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
        public abstract string FindBinary(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true);

        /// <summary>
        /// Attempts to locate a binary via the symbol server.  This function will then copy the file
        /// locally to the symbol cache and return the location of the local file on disk.
        /// </summary>
        /// <param name="module">The module to locate.</param>
        /// <param name="checkProperties">Whether or not to validate the properties of the binary after download.</param>
        /// <returns>A full path on disk (local) of where the binary was copied to, null if it was not found.</returns>
        public string FindBinary(ModuleInfo module, bool checkProperties = true)
        {
            return FindBinary(module.FileName, module.TimeStamp, module.FileSize, checkProperties);
        }

        /// <summary>
        /// Attempts to locate a dac via the symbol server.  This function will then copy the file
        /// locally to the symbol cache and return the location of the local file on disk.  Note that
        /// the dac should not validate if the properties of the file match the one it was indexed under.
        /// </summary>
        /// <param name="dac">The dac to locate.</param>
        /// <returns>A full path on disk (local) of where the binary was copied to, null if it was not found.</returns>
        public string FindBinary(DacInfo dac)
        {
            return FindBinary(dac, false);
        }

        /// <summary>
        /// Attempts to locate the pdb for a given module.
        /// </summary>
        /// <param name="module">The module to locate the pdb for.</param>
        /// <returns>A full path on disk (local) of where the pdb was copied to.</returns>
        public string FindPdb(ModuleInfo module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            PdbInfo pdb = module.Pdb;
            if (pdb == null)
                return null;

            return FindPdb(pdb);
        }

        /// <summary>
        /// Attempts to locate the pdb for a given module.
        /// </summary>
        /// <param name="pdb">The pdb to locate.</param>
        /// <returns>A full path on disk (local) of where the pdb was copied to.</returns>
        public string FindPdb(PdbInfo pdb)
        {
            if (pdb == null)
                throw new ArgumentNullException(nameof(pdb));

            return FindPdb(pdb.FileName, pdb.Guid, pdb.Revision);
        }

        /// <summary>
        /// Attempts to locate a pdb based on its name, guid, and revision number.
        /// </summary>
        /// <param name="pdbName">The name the pdb is indexed under.</param>
        /// <param name="pdbIndexGuid">The guid the pdb is indexed under.</param>
        /// <param name="pdbIndexAge">The age of the pdb.</param>
        /// <returns>A full path on disk (local) of where the pdb was copied to.</returns>
        public abstract string FindPdb(string pdbName, Guid pdbIndexGuid, int pdbIndexAge);

        /// <summary>
        /// Validates whether a pdb on disk matches the given Guid/revision.
        /// </summary>
        /// <param name="pdbName"></param>
        /// <param name="guid"></param>
        /// <param name="age"></param>
        /// <returns></returns>
        protected virtual bool ValidatePdb(string pdbName, Guid guid, int age)
        {
            try
            {
                PdbReader.GetPdbProperties(pdbName, out Guid fileGuid, out int fileAge);
                return guid == fileGuid && age == fileAge;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates whether a file on disk matches the properties we expect.
        /// </summary>
        /// <param name="fullPath">The full path on disk of a PEImage to inspect.</param>
        /// <param name="buildTimeStamp">The build timestamp we expect to match.</param>
        /// <param name="imageSize">The build image size we expect to match.</param>
        /// <param name="checkProperties">Whether we should actually validate the imagesize/timestamp or not.</param>
        /// <returns></returns>
        protected virtual bool ValidateBinary(string fullPath, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            if (string.IsNullOrEmpty(fullPath))
                return false;

            if (File.Exists(fullPath))
            {
                if (!checkProperties)
                {
                    return true;
                }

                try
                {
                    using (FileStream fs = File.OpenRead(fullPath))
                    {
                        if (Path.GetExtension(fullPath) == ".so")
                        {
                            Debug.WriteLine("Validate binary not yet implemented for .so!");
                            Debugger.Break();
                            return true;
                        }

                        PEImage peimage = new PEImage(fs, false);
                        if (peimage.IsValid)
                        {
                            if (!checkProperties || peimage.IndexTimeStamp == buildTimeStamp && peimage.IndexFileSize == imageSize)
                                return true;

                            Trace($"Rejected file '{fullPath}' because file size and time stamp did not match.");
                        }
                        else
                        {
                            Trace($"Rejected file '{fullPath}' because it is not a valid PE image.");
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace("Encountered exception {0} while attempting to inspect file '{1}'.", e.GetType().Name, fullPath);
                }
            }

            return false;
        }

        /// <summary>
        /// Copies a given stream to a file.
        /// </summary>
        /// <param name="input">The stream of data to copy.</param>
        /// <param name="fullSrcPath">The original source location of "stream".  This may be a URL or null.</param>
        /// <param name="fullDestPath">The full destination path to copy the file to.</param>
        /// <param name="size">A hint as to the length of the stream.  This may be 0 or negative if the length is unknown.</param>
        /// <returns>True if the method successfully copied the file, false otherwise.</returns>
        protected virtual void CopyStreamToFile(Stream input, string fullSrcPath, string fullDestPath, long size)
        {
            Debug.Assert(input != null);

            try
            {
                FileInfo fi = new FileInfo(fullDestPath);
                if (fi.Exists && fi.Length == size)
                    return;

                string folder = Path.GetDirectoryName(fullDestPath);
                Directory.CreateDirectory(folder);

                FileStream file = null;
                try
                {
                    file = new FileStream(fullDestPath, FileMode.OpenOrCreate);
                    byte[] buffer = new byte[2048];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        file.Write(buffer, 0, read);
                }
                finally
                {
                    if (file != null)
                        file.Dispose();
                }
            }
            catch (Exception e)
            {
                try
                {
                    if (File.Exists(fullDestPath))
                        File.Delete(fullDestPath);
                }
                catch
                {
                    // We ignore errors of this nature.
                }

                Trace("Encountered an error while attempting to copy '{0} to '{1}': {2}", fullSrcPath, fullDestPath, e.Message);
            }
        }

        /// <summary>
        /// Writes diagnostic messages about symbol loading to System.Diagnostics.Trace.  Figuring out symbol issues can be tricky,
        /// so if you override methods in SymbolLocator, be sure to trace the information here.
        /// </summary>
        /// <param name="fmt"></param>
        /// <param name="args"></param>
        protected virtual void Trace(string fmt, params object[] args)
        {
            if (args != null && args.Length > 0)
                fmt = string.Format(fmt, args);

            System.Diagnostics.Trace.WriteLine(fmt, "Microsoft.Diagnostics.Runtime.SymbolLocator");
        }

        /// <summary>
        /// Called when changing the symbol file path or cache.
        /// </summary>
        protected virtual void SymbolPathOrCacheChanged()
        {
            _missingPdbs.Clear();
            _missingFiles.Clear();
        }

        internal virtual void PrefetchBinary(string name, int timestamp, int imagesize)
        {
        }
    }
}