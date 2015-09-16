// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable 1591
#pragma warning disable 0067

namespace Microsoft.Diagnostics.Runtime.Utilities
{
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

        public SymbolLocator()
        {
            var sympath = _NT_SYMBOL_PATH;
            if (string.IsNullOrEmpty(sympath))
                sympath = MicrosoftSymbolServerPath;

            SymbolPath = sympath;
        }

        /// <summary>
        /// This allows you to set the _NT_SYMBOL_PATH as a string.  
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


        private List<SymPathElement> SymbolElements
        {
            get
            {
                if (_symbolElements == null)
                    _symbolElements = SymPathElement.GetElements(_symbolPath);

                return _symbolElements;
            }
        }

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

        public void ClearResultCache()
        {
            _binCache.Clear();
        }

        public delegate void FindPdbHandler(SymbolLocator sender, FindPdbEventArgs args);
        public delegate void FindBinaryHandler(SymbolLocator sender, FindBinaryEventArgs args);
        public delegate void CopyFileHandler(SymbolLocator sender, CopyFileEventArgs args);
        public delegate void ValidateBinaryHandler(SymbolLocator sender, ValidateBinaryEventArgs args);

        public event FindBinaryHandler SearchForBinary;
        public event FindPdbHandler SearchForPdb;
        public event CopyFileHandler CopyFile;
        public event ValidateBinaryHandler ValidateBinary;


        public string FindBinary(string fileName, uint buildTimeStamp, uint imageSize, bool checkProperties = true)
        {
            return FindBinary(fileName, (int)buildTimeStamp, (int)imageSize, checkProperties);
        }

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
            FindBinaryHandler evt = SearchForBinary;
            if (evt != null)
            {
                FindBinaryEventArgs args = new FindBinaryEventArgs(fullPath, buildTimeStamp, imageSize);
                foreach (FindBinaryHandler handler in evt.GetInvocationList())
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

        private static string GetIndexPath(string fileName, int buildTimeStamp, int imageSize)
        {
            return fileName + @"\" + buildTimeStamp.ToString("x") + imageSize.ToString("x") + @"\" + fileName;
        }

        private static string GetIndexPath(string pdbSimpleName, Guid pdbIndexGuid, int pdbIndexAge)
        {
            return pdbSimpleName + @"\" + pdbIndexGuid.ToString().Replace("-", "") + pdbIndexAge.ToString("x") + @"\" + pdbSimpleName;
        }
        public string GetCachedLocation(PdbInfo pdb)
        {
            return Path.Combine(SymbolCache, GetIndexPath(Path.GetFileName(pdb.FileName), pdb.Guid, pdb.Revision));
        }

        public string GetCachedLocation(ModuleInfo module)
        {
            string filename = module.FileName;
            uint timestamp = module.TimeStamp;
            uint imagesize = module.FileSize;
            return GetCachedLocation(filename, timestamp, imagesize);
        }

        public string GetCachedLocation(string filename, uint timestamp, uint imagesize)
        {
            return Path.Combine(SymbolCache, GetIndexPath(filename, (int)timestamp, (int)imagesize));
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
            return LoadPdb(pdb.FileName, pdb.Guid, pdb.Revision);
        }

        internal SymbolModule LoadPdb(string pdbName, Guid pdbIndexGuid, int pdbIndexAge)
        {
            // TODO: Should we not cache the result of this?
            string pdb = FindPdb(pdbName, pdbIndexGuid, pdbIndexAge);
            return LoadPdb(pdb);
        }

        public string FindPdb(ModuleInfo module)
        {
            return FindPdb(module.Pdb);
        }

        public string FindPdb(PdbInfo pdb)
        {
            return FindPdb(pdb.FileName, pdb.Guid, pdb.Revision);
        }

        public string FindPdb(string pdbName, Guid pdbIndexGuid, int pdbIndexAge)
        {
            if (string.IsNullOrEmpty(pdbName))
                return null;

            string pdbSimpleName = Path.GetFileName(pdbName);
            if (pdbName != pdbSimpleName)
            {
                if (PdbMatches(pdbName, pdbIndexGuid, pdbIndexAge))
                    return pdbName;
            }

            PdbEntry entry = new PdbEntry(pdbSimpleName, pdbIndexGuid, pdbIndexAge);
            string result = null;
            if (_pdbCache.TryGetValue(entry, out result))
                return result;


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
                    if (PdbMatches(fullPath, pdbIndexGuid, pdbIndexAge))
                    {
                        _pdbCache[entry] = fullPath;
                        return fullPath;
                    }
                }
            }

            return null;
        }

        public static bool PdbMatches(string filePath, Guid pdbIndexGuid, int revision)
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

                    if (pdbIndexGuid == session.globalScope.guid && (uint)revision == session.globalScope.age)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    if (source != null)
                        Marshal.ReleaseComObject(source);

                    if (session != null)
                        Marshal.ReleaseComObject(session);
                }
            }

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
                        WriteLine(LogLevel.Diagnostic, "Probe of {0} failed: {1}", fullUri, e.Message);
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

        public string DownloadBinary(ModuleInfo module, bool checkProperties = true)
        {
            return DownloadBinary(module.FileName, module.TimeStamp, module.FileSize, checkProperties);
        }

        public string DownloadBinary(DacInfo dac)
        {
            return DownloadBinary(dac, false);
        }

        public string DownloadBinary(string fileName, uint buildTimeStamp, uint imageSize, bool checkProperties = true)
        {
            return FindBinary(fileName, (int)buildTimeStamp, (int)imageSize, checkProperties);
        }

        public PEFile LoadBinary(string fileName, uint buildTimeStamp, uint imageSize, bool checkProperties = true)
        {
            string result = FindBinary(fileName, buildTimeStamp, imageSize, checkProperties);
            return LoadBinary(result);
        }

        public PEFile LoadBinary(string fileName)
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

        public enum LogLevel
        {
            Normal,
            Diagnostic
        }

        private void WriteLine(LogLevel level, string fmt, params object[] args)
        {
            // todo here-
            //Console.WriteLine(fmt, args);
        }

        private void WriteLine(string fmt, params object[] args)
        {
            WriteLine(LogLevel.Normal, fmt, args);
        }
    }

    public class FindPdbEventArgs
    {
        internal FindPdbEventArgs(string filename, Guid guid, int revision)
        {
            FileName = filename;
            Guid = guid;
            Revision = revision;
        }

        public string FileName { get; private set; }
        public Guid Guid { get; private set; }
        public int Revision { get; private set; }

        public string Result { get; set; }
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
        public string Result { get; set; }
    }

    public class ValidateBinaryEventArgs
    {
        public string File { get; private set; }
        public int FileSize { get; private set; }
        public int TimeStamp { get; private set; }
        public bool ValidateProperties { get; private set; }

        public bool Rejected { get; private set; }
        public bool Accepted { get; private set; }

        public void Reject()
        {
            Rejected = true;
        }

        public void Accept()
        {
            Accepted = true;
        }

        public ValidateBinaryEventArgs(string fileName, int timestamp, int fileSize, bool validate)
        {
            File = fileName;
            FileSize = fileSize;
            TimeStamp = timestamp;
            ValidateProperties = validate;
        }
    }

    public class CopyFileEventArgs
    {
        public string Source { get; private set; }
        public string Destination { get; private set; }
        public Stream Stream { get; private set; }
        public long Size { get; private set; }

        public bool IsCancelled { get; private set; }

        public bool IsComplete { get; private set; }

        public void Cancel()
        {
            IsCancelled = true;
        }

        public void Complete()
        {
            IsComplete = true;
        }

        public CopyFileEventArgs(string src, string dst, Stream stream, long size)
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
