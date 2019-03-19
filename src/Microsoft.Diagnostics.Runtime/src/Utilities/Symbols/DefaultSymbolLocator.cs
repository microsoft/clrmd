// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Default implementation of a symbol locator.
    /// </summary>
    public partial class DefaultSymbolLocator : SymbolLocator
    {
        /// <summary>
        /// Default implementation of finding a pdb.
        /// </summary>
        /// <param name="pdbName">The name the pdb is indexed under.</param>
        /// <param name="pdbIndexGuid">The guid the pdb is indexed under.</param>
        /// <param name="pdbIndexAge">The age of the pdb.</param>
        /// <returns>A full path on disk (local) of where the pdb was copied to.</returns>
        public override string FindPdb(string pdbName, Guid pdbIndexGuid, int pdbIndexAge)
        {
            if (string.IsNullOrEmpty(pdbName))
                return null;

            string pdbSimpleName = Path.GetFileName(pdbName);
            if (pdbName != pdbSimpleName)
            {
                if (ValidatePdb(pdbName, pdbIndexGuid, pdbIndexAge))
                    return pdbName;
            }

            // Check to see if it's already cached.
            PdbEntry entry = new PdbEntry(pdbSimpleName, pdbIndexGuid, pdbIndexAge);
            string result = GetPdbEntry(entry);
            if (result != null)
                return result;

            HashSet<PdbEntry> missingPdbs = _missingPdbs;
            if (IsMissing(missingPdbs, entry))
                return null;

            string pdbIndexPath = GetIndexPath(pdbSimpleName, pdbIndexGuid, pdbIndexAge);
            foreach (SymPathElement element in SymPathElement.GetElements(SymbolPath))
            {
                if (element.IsSymServer)
                {
                    string targetPath = TryGetFileFromServer(element.Target, pdbIndexPath, element.Cache ?? SymbolCache);
                    if (targetPath != null)
                    {
                        Trace("Found pdb {0} from server '{1}' on path '{2}'.  Copied to '{3}'.", pdbSimpleName, element.Target, pdbIndexPath, targetPath);
                        SetPdbEntry(missingPdbs, entry, targetPath);
                        return targetPath;
                    }

                    Trace("No matching pdb found on server '{0}' on path '{1}'.", element.Target, pdbIndexPath);
                }
                else
                {
                    string fullPath = Path.Combine(element.Target, pdbSimpleName);
                    if (ValidatePdb(fullPath, pdbIndexGuid, pdbIndexAge))
                    {
                        Trace($"Found pdb '{pdbSimpleName}' at '{fullPath}'.");
                        SetPdbEntry(missingPdbs, entry, fullPath);
                        return fullPath;
                    }

                    Trace($"Mismatched pdb found at '{fullPath}'.");
                }
            }

            SetPdbEntry(missingPdbs, entry, null);
            return null;
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
        public override string FindBinary(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true)
        {
            string fullPath = fileName;
            fileName = Path.GetFileName(fullPath).ToLower();

            // First see if we already have the result cached.
            FileEntry entry = new FileEntry(fileName, buildTimeStamp, imageSize);
            string result = GetFileEntry(entry);
            if (result != null)
                return result;

            HashSet<FileEntry> missingFiles = _missingFiles;
            if (IsMissing(missingFiles, entry))
                return null;

            // Test to see if the file is on disk.
            if (ValidateBinary(fullPath, buildTimeStamp, imageSize, checkProperties))
            {
                SetFileEntry(missingFiles, entry, fullPath);
                return fullPath;
            }

            // Finally, check the symbol paths.
            string exeIndexPath = null;
            string activeSymbolCache = SymbolCache;
            foreach (SymPathElement element in SymPathElement.GetElements(SymbolPath))
            {
                if (element.IsSymServer)
                {
                    if (exeIndexPath == null)
                        exeIndexPath = GetIndexPath(fileName, buildTimeStamp, imageSize);

                    string target = TryGetFileFromServer(element.Target, exeIndexPath, element.Cache ?? activeSymbolCache);
                    if (target == null)
                    {
                        Trace($"Server '{element.Target}' did not have file '{Path.GetFileName(fileName)}' with timestamp={buildTimeStamp:x} and filesize={imageSize:x}.");
                    }
                    else if (ValidateBinary(target, buildTimeStamp, imageSize, checkProperties))
                    {
                        Trace($"Found '{fileName}' on server '{element.Target}'.  Copied to '{target}'.");
                        SetFileEntry(missingFiles, entry, target);
                        return target;
                    }
                }
                else if (element.IsCache)
                {
                    if (!string.IsNullOrEmpty(element.Cache))
                        activeSymbolCache = element.Cache;
                    else
                        activeSymbolCache = SymbolCache;
                }
                else
                {
                    string filePath = Path.Combine(element.Target, fileName);
                    if (ValidateBinary(filePath, buildTimeStamp, imageSize, checkProperties))
                    {
                        Trace($"Found '{fileName}' at '{filePath}'.");
                        SetFileEntry(missingFiles, entry, filePath);
                        return filePath;
                    }
                }
            }

            SetFileEntry(missingFiles, entry, null);
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

        private string TryGetFileFromServer(string urlForServer, string fileIndexPath, string cache)
        {
            Debug.Assert(!string.IsNullOrEmpty(cache));
            if (string.IsNullOrEmpty(urlForServer))
                return null;

            string targetPath = Path.Combine(cache, fileIndexPath);

            // See if it is a compressed file by replacing the last character of the name with an _
            string compressedSigPath = fileIndexPath.Substring(0, fileIndexPath.Length - 1) + "_";
            string compressedFilePath = GetPhysicalFileFromServer(urlForServer, compressedSigPath, cache);
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
                    Trace("Exception encountered while expanding file '{0}': {1}", compressedFilePath, e.Message);
                }
                finally
                {
                    if (File.Exists(compressedFilePath))
                        File.Delete(compressedFilePath);
                }
            }

            // Just try to fetch the file directly
            string ret = GetPhysicalFileFromServer(urlForServer, fileIndexPath, cache);
            if (ret != null)
                return ret;

            // See if we have a file that tells us to redirect elsewhere. 
            string filePtrSigPath = Path.Combine(Path.GetDirectoryName(fileIndexPath), "file.ptr");
            string filePtrData = GetPhysicalFileFromServer(urlForServer, filePtrSigPath, cache, true);
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

            Trace("Error resolving file.ptr: content '{0}' from '{1}.", filePtrData, filePtrSigPath);

            return null;
        }

        private string GetPhysicalFileFromServer(string serverPath, string pdbIndexPath, string symbolCacheDir, bool returnContents = false)
        {
            if (string.IsNullOrEmpty(serverPath))
                return null;

            string fullDestPath = Path.Combine(symbolCacheDir, pdbIndexPath);
            if (File.Exists(fullDestPath))
                return fullDestPath;

            if (serverPath.StartsWith("http:") || serverPath.StartsWith("https:"))
            {
                string fullUri = serverPath + "/" + pdbIndexPath.Replace('\\', '/');
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(fullUri);
                    req.UserAgent = "Microsoft-Symbol-Server/6.13.0009.1140";
                    req.Timeout = Timeout;
                    WebResponse response = req.GetResponse();
                    using (Stream fromStream = response.GetResponseStream())
                    {
                        if (returnContents)
                        {
                            TextReader reader = new StreamReader(fromStream);
                            return reader.ReadToEnd();
                        }

                        CopyStreamToFile(fromStream, fullUri, fullDestPath, response.ContentLength);
                        return fullDestPath;
                    }
                }
                catch (WebException)
                {
                    // A timeout or 404.
                    return null;
                }
                catch (Exception e)
                {
                    Trace("Probe of {0} failed: {1}", fullUri, e.Message);
                    return null;
                }
            }

            string fullSrcPath = Path.Combine(serverPath, pdbIndexPath);
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

            return fullDestPath;
        }
    }
}