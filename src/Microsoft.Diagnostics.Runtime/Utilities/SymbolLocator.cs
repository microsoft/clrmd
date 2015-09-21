// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Utilities
{

    /// <summary>
    /// This class is a general purpose symbol locator and binary locator.
    /// </summary>
    public class SymbolLocator
    {
        private List<SymPathElement> _symbolElements = new List<SymPathElement>();
        private Dictionary<BinaryEntry, string> _binCache = new Dictionary<BinaryEntry, string>();
        private Dictionary<PdbEntry, string> _pdbCache = new Dictionary<PdbEntry, string>();
        private Dictionary<string, SymbolModule> _moduleCache = new Dictionary<string, SymbolModule>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, PEFile> _pefileCache = new Dictionary<string, PEFile>(StringComparer.OrdinalIgnoreCase);
        private string _symbolPath;
        private string _symbolCache;
        private static string[] s_microsoftSymbolServers = { "http://msdl.microsoft.com/download/symbols", "http://referencesource.microsoft.com/symbols" };

        /// <summary>
        /// Constructor.
        /// </summary>
        public SymbolLocator()
        {
            var sympath = _NT_SYMBOL_PATH;
            if (string.IsNullOrEmpty(sympath))
                sympath = MicrosoftSymbolServerPath;

            SymbolPath = sympath;
        }

        /// <summary>
        /// This property gets and sets the global _NT_SYMBOL_PATH environment variable.
        /// This is the global setting for symbol paths on a computer.
        /// </summary>
        public static string _NT_SYMBOL_PATH
        {
            get
            {
                var ret = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
                return ret ?? "";
            }
            set
            {
                Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", value);
            }
        }


        /// <summary>
        /// Retrieves a list of the default Microsoft symbol servers.
        /// </summary>
        public static string[] MicrosoftSymbolServers
        {
            get
            {
                return s_microsoftSymbolServers;
            }
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

                foreach (var path in s_microsoftSymbolServers)
                {
                    if (!first)
                        result.Append(';');

                    result.Append("SRV*");
                    result.Append(path);
                }

                return result.ToString();
            }
        }

        /// <summary>
        /// Gets or sets the SymbolPath this object uses to attempt to find PDBs and binaries.
        /// </summary>
        public string SymbolPath
        {
            get
            {
                return _symbolPath ?? "";
            }

            set
            {
                _symbolElements = null;
                _symbolPath = (value ?? "").Trim();
            }
        }

        /// <summary>
        /// Gets or sets the local symbol file cache.  This is the location that
        /// all symbol files are downloaded to on your computer.
        /// </summary>
        public string SymbolCache
        {
            get
            {
                if (!string.IsNullOrEmpty(_symbolCache))
                    return _symbolCache;

                string tmp = Path.GetTempPath();
                if (string.IsNullOrEmpty(tmp))
                    tmp = ".";

                return Path.Combine(tmp, "symbols");
            }
            set
            {
                _symbolCache = value;
                if (!string.IsNullOrEmpty(_symbolCache))
                    Directory.CreateDirectory(_symbolCache);
            }
        }
        
        /// <summary>
        /// This is called when SymbolLocator cannot find a PDB, giving the user the
        /// chance to locate the pdb manually.
        /// </summary>
        /// <param name="sender">The SymbolLocator attempting to find the pdb.</param>
        /// <param name="args">Information about the PDB we are attempting to locate.</param>
        public delegate void PdbNotFoundHandler(SymbolLocator sender, FindPdbEventArgs args);

        /// <summary>
        /// This is called when SymbolLocator cannot find a file, giving the user the chance
        /// to locate the file manually.
        /// </summary>
        /// <param name="sender">The SymbolLocator attempting to find the pdb.</param>
        /// <param name="args">Information about the PDB we are attempting to locate.</param>
        public delegate void BinaryNotFoundHandler(SymbolLocator sender, FindBinaryEventArgs args);

        /// <summary>
        /// This is called before SymbolLocator attempts to copy a file, giving the user a chance
        /// to override file copy.
        /// </summary>
        /// <param name="sender">The SymbolLocator that is about to copy a file.</param>
        /// <param name="args">Information about the source/destination of the file to be copied.</param>
        public delegate void CopyFileHandler(SymbolLocator sender, CopyFileEventArgs args);

        /// <summary>
        /// Called when SymbolLocator needs to validate if a binary is the correct one or not.
        /// </summary>
        /// <param name="sender">The SymbolLocator attempting to validate a binary.</param>
        /// <param name="args">Information about the file needing to be validated.</param>
        public delegate void ValidateBinaryHandler(SymbolLocator sender, ValidateBinaryEventArgs args);

        /// <summary>
        /// Called when SymbolLocator needs to validate if a pdb is the correct one or not.
        /// </summary>
        /// <param name="sender">The SymbolLocator attempting to validate a pdb.</param>
        /// <param name="args">Information about the pdb needing to be validated.</param>
        public delegate void ValidatePdbHandler(SymbolLocator sender, ValidatePdbEventArgs args);

        /// <summary>
        /// This is called when SymbolLocator cannot find a file, giving the user the chance
        /// to locate the file manually.
        /// </summary>
        public event BinaryNotFoundHandler BinaryNotFound;
        /// <summary>
        /// This is called when SymbolLocator cannot find a PDB, giving the user the
        /// chance to locate the pdb manually.
        /// </summary>
        public event PdbNotFoundHandler PdbNotFound;
        /// <summary>
        /// This is called before SymbolLocator attempts to copy a file, giving the user a chance
        /// to override file copy.
        /// </summary>
        public event CopyFileHandler CopyFile;
        /// <summary>
        /// Called when SymbolLocator needs to validate if a binary is the correct one or not.
        /// </summary>
        public event ValidateBinaryHandler ValidateBinary;
        /// <summary>
        /// Called when SymbolLocator needs to validate if a pdb is the correct one or not.
        /// </summary>
        public event ValidatePdbHandler ValidatePdb;

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
        public string FindBinary(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true)
        {
            string fullPath = fileName;
            fileName = Path.GetFileName(fullPath).ToLower();

            BinaryEntry entry = new BinaryEntry(fileName, buildTimeStamp, imageSize);
            string result;
            if (_binCache.TryGetValue(entry, out result))
            {
                Debug.Assert(result != null);
                if (File.Exists(result))
                    return result;

                _binCache.Remove(entry);
            }

            // First ask any file locator handlers to find it.
            BinaryNotFoundHandler evt = BinaryNotFound;
            if (evt != null)
            {
                FindBinaryEventArgs args = new FindBinaryEventArgs(fullPath, buildTimeStamp, imageSize);
                foreach (BinaryNotFoundHandler handler in evt.GetInvocationList())
                {
                    handler(this, args);

                    if (!string.IsNullOrEmpty(args.Result))
                    {
                        if (CheckPathOnDisk(args.Result, buildTimeStamp, imageSize, checkProperties))
                        {
                            WriteLine("Custom handler returned path '{0}' for file {1}.", args.Result, fileName);
                            _binCache[entry] = args.Result;
                            return args.Result;
                        }

                        WriteLine("Search for file {0} returned rejected file {1}.", fullPath, args.Result);
                        args.Result = null;
                    }
                }
            }

            // Test to see if the file is on disk.
            if (CheckPathOnDisk(fullPath, buildTimeStamp, imageSize, checkProperties))
            {
                _binCache[entry] = result;
                return result;
            }

            // Finally, check the symbol paths.
            string exeIndexPath = null;
            foreach (SymPathElement element in SymbolElements)
            {
                if (element.IsSymServer)
                {
                    if (exeIndexPath == null)
                        exeIndexPath = GetIndexPath(fileName, buildTimeStamp, imageSize);

                    string target = TryGetFileFromServer(element.Target, exeIndexPath, element.Cache ?? SymbolCache);
                    if (CheckPathOnDisk(target, buildTimeStamp, imageSize, checkProperties))
                    {
                        _binCache[entry] = target;
                        return target;
                    }
                }
                else
                {
                    string filePath = Path.Combine(element.Target, fileName);
                    if (CheckPathOnDisk(filePath, buildTimeStamp, imageSize, checkProperties))
                    {
                        _binCache[entry] = filePath;
                        return filePath;
                    }
                }
            }

            // Found nothing.
            return null;
        }

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
                throw new ArgumentNullException("module");

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
                throw new ArgumentNullException("pdb");

            return FindPdb(pdb.FileName, pdb.Guid, pdb.Revision);
        }

        /// <summary>
        /// Attempts to locate a pdb based on its name, guid, and revision number.
        /// </summary>
        /// <param name="pdbName">The name the pdb is indexed under.</param>
        /// <param name="pdbIndexGuid">The guid the pdb is indexed under.</param>
        /// <param name="pdbIndexAge">The age of the pdb.</param>
        /// <returns>A full path on disk (local) of where the pdb was copied to.</returns>
        public string FindPdb(string pdbName, Guid pdbIndexGuid, int pdbIndexAge)
        {
            if (string.IsNullOrEmpty(pdbName))
                return null;

            string pdbSimpleName = Path.GetFileName(pdbName);
            if (pdbName != pdbSimpleName)
            {
                if (CheckPdb(pdbName, pdbIndexGuid, pdbIndexAge))
                    return pdbName;
            }

            PdbEntry entry = new PdbEntry(pdbSimpleName, pdbIndexGuid, pdbIndexAge);
            string result = null;
            if (_pdbCache.TryGetValue(entry, out result))
                return result;

            // Ask any search handlers to look for it.
            PdbNotFoundHandler evt = PdbNotFound;
            if (evt != null)
            {
                FindPdbEventArgs args = new FindPdbEventArgs(pdbName, pdbIndexGuid, pdbIndexAge);
                foreach (PdbNotFoundHandler handler in evt.GetInvocationList())
                {
                    handler(this, args);
                    if (!string.IsNullOrEmpty(args.Result))
                    {
                        Debug.Assert(File.Exists(args.Result));
                        _pdbCache[entry] = args.Result;
                        return args.Result;
                    }
                }
            }

            string pdbIndexPath = null;
            foreach (SymPathElement element in SymbolElements)
            {
                if (element.IsSymServer)
                {
                    if (pdbIndexPath == null)
                        pdbIndexPath = GetIndexPath(pdbSimpleName, pdbIndexGuid, pdbIndexAge);

                    string targetPath = TryGetFileFromServer(element.Target, pdbIndexPath, element.Cache ?? SymbolCache);
                    if (targetPath != null)
                    {
                        WriteLine("Found pdb {0} from server '{1}' on path '{2}'.  Copied to '{3}'.", pdbSimpleName, element.Target, pdbIndexPath, targetPath);
                        _pdbCache[entry] = targetPath;
                        return targetPath;
                    }
                    else
                    {
                        WriteLine("No matching pdb found on server '{0}' on path '{1}'.", element.Target, pdbIndexPath);
                    }
                }
                else
                {
                    string fullPath = Path.Combine(element.Target, pdbSimpleName);
                    if (CheckPdb(fullPath, pdbIndexGuid, pdbIndexAge))
                    {
                        _pdbCache[entry] = fullPath;
                        return fullPath;
                    }
                }
            }

            return null;
        }

        private bool CheckPdb(string pdbName, Guid guid, int revision)
        {
            ValidatePdbHandler evt = ValidatePdb;

            if (evt != null)
            {
                ValidatePdbEventArgs args = new ValidatePdbEventArgs(pdbName, guid, revision);

                foreach (ValidatePdbHandler handler in evt.GetInvocationList())
                {
                    handler(this, args);

                    if (args.Accepted)
                        return true;

                    if (args.Rejected)
                        return false;
                }
            }
            
            return PdbMatches(pdbName, guid, revision);
        }

        private static string GetIndexPath(string fileName, int buildTimeStamp, int imageSize)
        {
            return fileName + @"\" + buildTimeStamp.ToString("x") + imageSize.ToString("x") + @"\" + fileName;
        }

        private static string GetIndexPath(string pdbSimpleName, Guid pdbIndexGuid, int pdbIndexAge)
        {
            return pdbSimpleName + @"\" + pdbIndexGuid.ToString().Replace("-", "") + pdbIndexAge.ToString("x") + @"\" + pdbSimpleName;
        }

        private List<SymPathElement> SymbolElements
        {
            get
            {
                if (_symbolElements == null)
                    _symbolElements = SymPathElement.GetElements(_symbolPath);

                return _symbolElements;
            }
        }

        internal SymbolModule LoadPdb(string pdbPath)
        {
            if (string.IsNullOrEmpty(pdbPath))
                return null;

            SymbolModule result;
            if (_moduleCache.TryGetValue(pdbPath, out result))
            {
                // TODO:  Add dispose on SymbolModule
                //if (!result.Disposed)

                return result;
            }

            result = new SymbolModule(new SymbolReader(null, null), pdbPath);
            _moduleCache[pdbPath] = result;

            return result;
        }

        internal SymbolModule LoadPdb(ModuleInfo module)
        {
            var pdb = module.Pdb;
            if (pdb == null)
                return null;

            return LoadPdb(pdb.FileName, pdb.Guid, pdb.Revision);
        }

        internal SymbolModule LoadPdb(string pdbName, Guid pdbIndexGuid, int pdbIndexAge)
        {
            // TODO: Should we not cache the result of this?
            string pdb = FindPdb(pdbName, pdbIndexGuid, pdbIndexAge);
            return LoadPdb(pdb);
        }

        /// <summary>
        /// Determines if a given pdb on disk matches a given Guid and age.
        /// </summary>
        /// <param name="filePath">The path on disk of a pdb to check.</param>
        /// <param name="guid">The guid to compare to.</param>
        /// <param name="age">The age to compare to.</param>
        /// <returns>True if they match, false if they do not.</returns>
        public static bool PdbMatches(string filePath, Guid guid, int age)
        {
            Guid fileGuid;
            int fileAge;
            if (!GetPdbInfo(filePath, out fileGuid, out fileAge))
                return false;

            return guid == fileGuid && age == fileAge;
        }

        /// <summary>
        /// Returns the guid and age of a pdb on disk.
        /// </summary>
        /// <param name="filePath">The pdb on disk to load.</param>
        /// <param name="guid">The guid of the pdb on disk.</param>
        /// <param name="age">The age of the pdb on disk.</param>
        /// <returns>True if the information was successfully loaded, false if the pdb could not be found or loaded.</returns>
        public static bool GetPdbInfo(string filePath, out Guid guid, out int age)
        {
            if (File.Exists(filePath))
            {
                Dia2Lib.IDiaDataSource source = null;
                Dia2Lib.IDiaSession session = null;
                try
                {
                    source = DiaLoader.GetDiaSourceObject();
                    source.loadDataFromPdb(filePath);
                    source.openSession(out session);
                    
                    guid = session.globalScope.guid;
                    age = (int)session.globalScope.age;
                    return true;
                }
                catch (Exception)
                {
                    // TODO: This should be a more specific catch.
                }
                finally
                {
                    if (source != null)
                        Marshal.ReleaseComObject(source);

                    if (session != null)
                        Marshal.ReleaseComObject(session);
                }
            }

            guid = Guid.Empty;
            age = 0;
            return false;
        }

        private string TryGetFileFromServer(string urlForServer, string fileIndexPath, string cache)
        {
            Debug.Assert(!string.IsNullOrEmpty(cache));
            if (String.IsNullOrEmpty(urlForServer))
                return null;

            // Just try to fetch the file directly
            var ret = GetPhysicalFileFromServer(urlForServer, fileIndexPath, cache);
            if (ret != null)
                return ret;

            var targetPath = Path.Combine(cache, fileIndexPath);

            // See if it is a compressed file by replacing the last character of the name with an _
            var compressedSigPath = fileIndexPath.Substring(0, fileIndexPath.Length - 1) + "_";
            var compressedFilePath = GetPhysicalFileFromServer(urlForServer, compressedSigPath, cache);
            if (compressedFilePath != null)
            {
                try
                {
                    // Decompress it
                    Command.Run("Expand " + Command.Quote(compressedFilePath) + " " + Command.Quote(targetPath));
                    return targetPath;
                }
                catch (Exception e)
                {
                    WriteLine("Exception encountered while expanding file '{0}': {1}", compressedFilePath, e.Message);
                }
                finally
                {
                    if (File.Exists(compressedFilePath))
                        File.Delete(compressedFilePath);
                }
            }

            // See if we have a file that tells us to redirect elsewhere. 
            var filePtrSigPath = Path.Combine(Path.GetDirectoryName(fileIndexPath), "file.ptr");
            var filePtrData = GetPhysicalFileFromServer(urlForServer, filePtrSigPath, cache, true);
            if (filePtrData == null)
            {
                return null;
            }

            filePtrData = filePtrData.Trim();
            if (filePtrData.StartsWith("PATH:"))
                filePtrData = filePtrData.Substring(5);

            if (!filePtrData.StartsWith("MSG:") && File.Exists(filePtrData))
            {
                using (FileStream fs = File.OpenRead(filePtrData))
                {
                    CopyStreamToFile(fs, filePtrData, targetPath, fs.Length);
                    return targetPath;
                }
            }
            else
            {
                WriteLine("Error resolving file.ptr: content '{0}' from '{1}.", filePtrData, filePtrSigPath);
            }

            return null;
        }

        private string GetPhysicalFileFromServer(string serverPath, string pdbIndexPath, string symbolCacheDir, bool returnContents = false)
        {
            if (string.IsNullOrEmpty(serverPath))
                return null;

            var fullDestPath = Path.Combine(symbolCacheDir, pdbIndexPath);
            if (!File.Exists(fullDestPath))
            {
                if (serverPath.StartsWith("http:"))
                {
                    var fullUri = serverPath + "/" + pdbIndexPath.Replace('\\', '/');
                    try
                    {
                        var req = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(fullUri);
                        req.UserAgent = "Microsoft-Symbol-Server/6.13.0009.1140";
                        var response = req.GetResponse();
                        using (var fromStream = response.GetResponseStream())
                        {
                            if (returnContents)
                            {
                                TextReader reader = new StreamReader(fromStream);
                                return reader.ReadToEnd();
                            }

                            CopyStreamToFile(fromStream, fullUri, fullDestPath, response.ContentLength);
                        }
                    }
                    catch (Exception e)
                    {
                        WriteLine("Probe of {0} failed: {1}", fullUri, e.Message);
                        return null;
                    }
                }
                else
                {
                    var fullSrcPath = Path.Combine(serverPath, pdbIndexPath);
                    if (!File.Exists(fullSrcPath))
                        return null;

                    if (returnContents)
                    {
                        try
                        {
                            return File.ReadAllText(fullSrcPath);
                        }
                        catch
                        {
                            return "";
                        }
                    }

                    using (FileStream fs = File.OpenRead(fullSrcPath))
                        CopyStreamToFile(fs, fullSrcPath, fullDestPath, fs.Length);
                }
            }
            else
            {
                WriteLine("Found file {0} in cache.", fullDestPath);
            }

            return fullDestPath;
        }

        private bool CopyStreamToFile(Stream stream, string fullSrcPath, string fullDestPath, long size)
        {
            Debug.Assert(stream != null);

            var evt = CopyFile;
            if (evt != null)
            {
                CopyFileEventArgs args = new CopyFileEventArgs(fullSrcPath, fullDestPath, stream, size);

                foreach (CopyFileHandler func in evt.GetInvocationList())
                {
                    func(this, args);

                    if (args.IsCancelled)
                        return false;
                    else if (args.IsComplete)
                        return File.Exists(fullDestPath);
                }
            }

            try
            {
                FileInfo fi = new FileInfo(fullDestPath);
                if (fi.Exists && fi.Length == size)
                    return true;

                string folder = Path.GetDirectoryName(fullDestPath);
                Directory.CreateDirectory(folder);

                FileStream file = null;
                try
                {
                    file = new FileStream(fullDestPath, FileMode.OpenOrCreate);
                    byte[] buffer = new byte[2048];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        file.Write(buffer, 0, read);
                }
                catch (IOException)
                {
                    return false;
                }
                finally
                {
                    if (file != null)
                        file.Dispose();
                }

                return true;
            }
            catch (Exception e)
            {
                SafeDeleteFile(fullDestPath);

                WriteLine("Encountered an error while attempting to copy '{0} to '{1}': {2}", fullSrcPath, fullDestPath, e.Message);
                return false;
            }
        }

        private static void SafeDeleteFile(string fullDestPath)
        {
            try
            {
                if (File.Exists(fullDestPath))
                    File.Delete(fullDestPath);
            }
            catch
            {
                // We will ignore errors of this nature.
            }
        }

        internal PEFile LoadBinary(string fileName, uint buildTimeStamp, uint imageSize, bool checkProperties = true)
        {
            string result = FindBinary(fileName, buildTimeStamp, imageSize, checkProperties);
            return LoadBinary(result);
        }

        internal PEFile LoadBinary(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            PEFile result;
            if (_pefileCache.TryGetValue(fileName, out result))
            {
                // TODO: Add .Disposed property.
                //if (!result.Disposed)
                return result;
            }

            try
            {
                result = new PEFile(fileName);
                _pefileCache[fileName] = result;
            }
            catch
            {
                WriteLine("Failed to load PEFile '{0}'.", fileName);
            }

            return result;
        }


        private bool CheckPathOnDisk(string fullPath, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            if (string.IsNullOrEmpty(fullPath))
                return false;

            if (File.Exists(fullPath))
            {
                var evt = ValidateBinary;
                if (evt != null)
                {
                    var args = new ValidateBinaryEventArgs(fullPath, buildTimeStamp, imageSize, checkProperties);

                    foreach (ValidateBinaryHandler func in evt.GetInvocationList())
                    {
                        func(this, args);

                        if (args.Rejected)
                            return false;

                        if (args.Accepted)
                            return true;
                    }
                }

                if (!checkProperties)
                {
                    WriteLine("Found '{0}' for file {1}.", fullPath, Path.GetFileName(fullPath));
                    return true;
                }

                try
                {
                    using (PEFile pefile = new PEFile(fullPath))
                    {
                        var header = pefile.Header;
                        if (!checkProperties || (header.TimeDateStampSec == buildTimeStamp && header.SizeOfImage == imageSize))
                        {
                            WriteLine("Found '{0}' for file {1}.", fullPath, Path.GetFileName(fullPath));
                            return true;
                        }
                        else
                        {
                            WriteLine("Rejected file '{0}' because file size and time stamp did not match.", fullPath);
                        }
                    }
                }
                catch (Exception e)
                {
                    WriteLine("Encountered exception {0} while attempting to inspect file '{1}'.", e.GetType().Name, fullPath);
                }
            }

            return false;
        }

        private void WriteLine(string fmt, params object[] args)
        {
            // todo here-
            //Console.WriteLine(fmt, args);
        }
    }
    

    /// <summary>
    /// Arguments given when SymbolLocator attempts to locate a pdb but cannot
    /// find it.
    /// </summary>
    public class FindPdbEventArgs
    {
        internal FindPdbEventArgs(string filename, Guid guid, int revision)
        {
            FileName = filename;
            Guid = guid;
            Revision = revision;
        }

        /// <summary>
        /// The filename of the pdb.  This may be a full path that was baked into the image and not
        /// just a simple "foo.pdb" name.
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// The Guid of the PDB to compare to.
        /// </summary>
        public Guid Guid { get; private set; }

        /// <summary>
        /// The age of the pdb as baked into the image.
        /// </summary>
        public int Revision { get; private set; }

        /// <summary>
        /// The result of the operation.
        /// </summary>
        public string Result { get; private set; }

        /// <summary>
        /// Call this if you have located the pdb.
        /// </summary>
        /// <param name="pdb">The full path on disk to load the pdb from.</param>
        public void SetPdbLocation(string pdb)
        {
            Debug.Assert(File.Exists(pdb));
            Result = pdb;
        }
    }


    /// <summary>
    /// Arguments given when ClrMD attempts to locate a binary file.
    /// </summary>
    public class FindBinaryEventArgs
    {
        internal FindBinaryEventArgs(string fileName, int timeStamp, int fileSize)
        {
            FileName = fileName;
            TimeStamp = timeStamp;
            FileSize = fileSize;
        }

        /// <summary>
        /// The name of the file we are attempting to locate.  Note this property may be a full
        /// path on disk or a simple file name.  A full path on disk should be treated as a hint
        /// as to where to look for the file.
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// The time stamp of the file we are attempting to locate.
        /// </summary>
        public int TimeStamp { get; private set; }

        /// <summary>
        /// The file size of the file we are attempting to locate.
        /// </summary>
        public int FileSize { get; private set; }

        /// <summary>
        /// If you located the file, fill this property with the full path on disk of where to find it.
        /// Note that this must be a local, accessable path and not a URL.
        /// </summary>
        public string Result { get; internal set; }

        /// <summary>
        /// Call this if you have located the file requested.
        /// </summary>
        /// <param name="file">The full path to the file on disk.</param>
        public void SetBinaryLocation(string file)
        {
            Debug.Assert(File.Exists(file));
            Result = file;
        }
    }

    /// <summary>
    /// Arguments for when the SymbolLocator needs to validate a given file/pdb is the one we are looking
    /// for.  You should call Accept if the file is the correct file we are looking for, call Reject if
    /// the file is not the correct one we are looking for, or call neither if your handler cannot handle
    /// this file.
    /// </summary>
    public class ValidateEventArgs
    {
        /// <summary>
        /// The file we need to validate.  This may be a remote file on another machine (\\foo\bar.dll)
        /// or a local file, but it will never be something on a URL (http://foo/bar).  The file will be
        /// downloaded before asking for validation if it's not a UNC path.
        /// </summary>
        public string File { get; private set; }

        /// <summary>
        /// Whether or not the file was rejected.
        /// </summary>
        public bool Rejected { get; private set; }

        /// <summary>
        /// Whether or not the file was accepted.
        /// </summary>
        public bool Accepted { get; private set; }

        /// <summary>
        /// Rejects this file as not the one we are searching for.
        /// </summary>
        public void Reject()
        {
            Debug.Assert(!Accepted);
            Rejected = true;
        }

        /// <summary>
        /// Accepts this file, meaning it is the one we are searching for.
        /// </summary>
        public void Accept()
        {
            Debug.Assert(!Rejected);
            Accepted = true;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="file">The file to validate.</param>
        public ValidateEventArgs(string file)
        {
            Debug.Assert(System.IO.File.Exists(file));
            File = file;
        }
    }

    /// <summary>
    /// Used when attempting to validate that a binary is the one we are searching for.  Note that
    /// unlike PDBs, we can explictly request that you do NOT validate the FileSize and TimeStamp
    /// of a binary.  (Specifically, the DAC should not be validated, as it is indexed under properties
    /// different from its own.)  In that case, we still fire the event with these properties to give
    /// an opportunity to reject the file for other reasons.
    /// </summary>
    public class ValidateBinaryEventArgs : ValidateEventArgs
    {
        /// <summary>
        /// The "filesize" of the binary we are attempting to match.  This corresponds to the
        /// IMAGE_FILE_HEADER::TimeDateStamp value in the PE header of the file.  In ClrMD's
        /// implementation, this is PEFile.Header.TimeDateStampSec.
        /// </summary>
        public int FileSize { get; private set; }

        /// <summary>
        /// The build timestamp of the binary we are attempting to match.  This corresponds to
        /// IMAGE_OPTIONAL_HEADER::SizeOfImage value in the PE header of the file.  In ClrMD's
        /// implementation, this is PEFile.Header.SizeOfImage.
        /// </summary>
        public int TimeStamp { get; private set; }

        /// <summary>
        /// Whether or not the runtime has asked to validate properties on the given file.
        /// If this is set to false, it is likely that the target file is not even supposed
        /// to match the filesize/timestamp of the given properties.
        /// </summary>
        public bool ValidateProperties { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="fileName">The file on disk.</param>
        /// <param name="timestamp">The timestamp to check against.</param>
        /// <param name="fileSize">The filesize to compare to.</param>
        /// <param name="validate">Whether to validate properties.</param>
        public ValidateBinaryEventArgs(string fileName, int timestamp, int fileSize, bool validate)
            : base(fileName)
        {
            FileSize = fileSize;
            TimeStamp = timestamp;
            ValidateProperties = validate;
        }
    }

    /// <summary>
    /// Called when attempting to validate whether a PDB is the one we are looking for.
    /// </summary>
    public class ValidatePdbEventArgs : ValidateEventArgs
    {
        /// <summary>
        /// The Guid of the PDB.
        /// </summary>
        public Guid Guid { get; private set; }

        /// <summary>
        /// The age of the given pdb as baked into the image.
        /// </summary>
        public int Revision { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="fileName">The file on disk to validate.</param>
        /// <param name="guid">The guid of the pdb.</param>
        /// <param name="revision">The age of the pdb.</param>
        public ValidatePdbEventArgs(string fileName, Guid guid, int revision)
            : base(fileName)
        {
            Guid = guid;
            Revision = revision;
        }
    }

    /// <summary>
    /// Called when clrmd is attempting to copy a stream to a given file location.
    /// This is given as an option to allow you to override ClrMD's copy mechanism.
    /// </summary>
    public class CopyFileEventArgs
    {
        /// <summary>
        /// The source location of the file.  This may be a URL (http://) or it may
        /// be a UNC path.  This is informational only, as you should be using
        /// the Stream property to copy from the stream.
        /// </summary>
        public string Source { get; private set; }

        /// <summary>
        /// The destination location of where the file should be copied to.
        /// </summary>
        public string Destination { get; private set; }

        /// <summary>
        /// The actual stream that needs to be copied to a file.
        /// </summary>
        public Stream Stream { get; private set; }

        /// <summary>
        /// The length of the file needing to be copied.  This may be -1 if the
        /// size has not been calculated.
        /// </summary>
        public long Size { get; private set; }

        /// <summary>
        /// Returns whether the copy has been cancelled or not.
        /// </summary>
        public bool IsCancelled { get; private set; }

        /// <summary>
        /// Returns whether the copy has been completed or not.
        /// </summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// Cancels the file copy.
        /// </summary>
        public void Cancel()
        {
            IsCancelled = true;
        }

        /// <summary>
        /// Marks the file copy as complete, and that Destination has the location of the file.
        /// </summary>
        public void Complete()
        {
            IsComplete = true;
        }
        
        internal CopyFileEventArgs(string src, string dst, Stream stream, long size)
        {
            Source = src;
            Destination = dst;
            Stream = stream;
            Size = size;
        }
    }

    internal struct BinaryEntry : IEquatable<BinaryEntry>
    {
        public string FileName;
        public int TimeStamp;
        public int FileSize;

        public BinaryEntry(string filename, int timestamp, int filesize)
        {
            FileName = filename;
            TimeStamp = timestamp;
            FileSize = filesize;
        }

        public override int GetHashCode()
        {
            return FileName.ToLower().GetHashCode() ^ TimeStamp ^ FileSize;
        }

        public override bool Equals(object obj)
        {
            return obj is BinaryEntry && Equals((BinaryEntry)obj);
        }

        public bool Equals(BinaryEntry other)
        {
            return FileName.Equals(other.FileName, StringComparison.OrdinalIgnoreCase) && TimeStamp == other.TimeStamp && FileSize == other.FileSize;
        }
    }

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
