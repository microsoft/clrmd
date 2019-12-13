// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// PEFile is a reader for the information in a Portable Exectable (PE) FILE.   This is what EXEs and DLLs are.
    /// It can read both 32 and 64 bit PE files.
    /// </summary>
    [Obsolete("Use PEImage instead.")]
    public sealed unsafe class PEFile : IDisposable
    {
        /// <summary>
        /// Parses a PEFile from a given stream. If it is valid, a new PEFile object is
        /// constructed and returned. Otherwise, null is returned.
        /// </summary>
        public static PEFile TryLoad(Stream stream, bool virt)
        {
            PEBuffer headerBuff = new PEBuffer(stream);
            PEHeader hdr = PEHeader.FromBuffer(headerBuff, virt);

            if (hdr == null)
                return null;

            PEFile pefile = new PEFile();
            pefile.Init(stream, "stream", virt, headerBuff, hdr);
            return pefile;
        }

        private PEFile()
        {
        }

        /// <summary>
        /// Create a new PEFile header reader.
        /// </summary>
        /// <param name="filePath">The path to the file on disk.</param>
        public PEFile(string filePath)
        {
            Init(File.OpenRead(filePath), filePath, false);
        }

        /// <summary>
        /// Constructor which allows you to specify a stream instead of file on disk.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="virt">
        /// Whether the stream is currently in virtual memory (true)
        /// or if this reading from disk (false).
        /// </param>
        public PEFile(Stream stream, bool virt)
        {
            Init(stream, "stream", virt);
        }

        private void Init(Stream stream, string filePath, bool virt, PEBuffer buffer = null, PEHeader header = null)
        {
            if (buffer == null)
                buffer = new PEBuffer(stream);

            if (header == null)
                header = PEHeader.FromBuffer(buffer, virt);

            _virt = virt;
            _stream = stream;
            _headerBuff = buffer;
            Header = header;
            if (header != null && header.PEHeaderSize > _headerBuff.Length)
                throw new InvalidOperationException("Bad PE Header in " + filePath);
        }

        /// <summary>
        /// The Header for the PE file.  This contains the infor in a link /dump /headers
        /// </summary>
        public PEHeader Header { get; private set; }

        /// <summary>
        /// Looks up the debug signature information in the EXE.   Returns true and sets the parameters if it is found.
        /// If 'first' is true then the first entry is returned, otherwise (by default) the last entry is used
        /// (this is what debuggers do today).   Thus NGEN images put the IL PDB last (which means debuggers
        /// pick up that one), but we can set it to 'first' if we want the NGEN PDB.
        /// </summary>
        public bool GetPdbSignature(out string pdbName, out Guid pdbGuid, out int pdbAge, bool first = false)
        {
            pdbName = null;
            pdbGuid = Guid.Empty;
            pdbAge = 0;
            bool ret = false;

            if (Header == null)
                return false;

            if (Header.DebugDirectory.VirtualAddress != 0)
            {
                PEBuffer buff = AllocBuff();
                IMAGE_DEBUG_DIRECTORY* debugEntries = (IMAGE_DEBUG_DIRECTORY*)FetchRVA((int)Header.DebugDirectory.VirtualAddress, (int)Header.DebugDirectory.Size, buff);
                if (Header.DebugDirectory.Size % sizeof(IMAGE_DEBUG_DIRECTORY) != 0)
                    return false;

                int debugCount = (int)Header.DebugDirectory.Size / sizeof(IMAGE_DEBUG_DIRECTORY);
                for (int i = 0; i < debugCount; i++)
                {
                    if (debugEntries[i].Type == IMAGE_DEBUG_TYPE.CODEVIEW)
                    {
                        PEBuffer stringBuff = AllocBuff();
                        int ptr = _virt ? debugEntries[i].AddressOfRawData : debugEntries[i].PointerToRawData;
                        CV_INFO_PDB70* info = (CV_INFO_PDB70*)stringBuff.Fetch(ptr, debugEntries[i].SizeOfData);
                        if (info->CvSignature == CV_INFO_PDB70.PDB70CvSignature)
                        {
                            // If there are several this picks the last one.  
                            pdbGuid = info->Signature;
                            pdbAge = info->Age;
                            pdbName = info->PdbFileName;
                            ret = true;
                            if (first)
                                break;
                        }

                        FreeBuff(stringBuff);
                    }
                }

                FreeBuff(buff);
            }

            return ret;
        }

        private PdbInfo _pdb;
        /// <summary>
        /// Holds information about the pdb for the current PEFile
        /// </summary>
        public PdbInfo PdbInfo
        {
            get
            {
                if (_pdb == null && GetPdbSignature(out string pdbName, out Guid pdbGuid, out int pdbAge))
                    _pdb = new PdbInfo(pdbName, pdbGuid, pdbAge);

                return _pdb;
            }
        }

        internal static bool TryGetIndexProperties(string filename, out int timestamp, out int filesize)
        {
            try
            {
                using (PEFile pefile = new PEFile(filename))
                {
                    PEHeader header = pefile.Header;
                    timestamp = header.TimeDateStampSec;
                    filesize = (int)header.SizeOfImage;
                    return true;
                }
            }
            catch
            {
                timestamp = 0;
                filesize = 0;
                return false;
            }
        }

        internal static bool TryGetIndexProperties(Stream stream, bool virt, out int timestamp, out int filesize)
        {
            try
            {
                using (PEFile pefile = new PEFile(stream, virt))
                {
                    PEHeader header = pefile.Header;
                    timestamp = header.TimeDateStampSec;
                    filesize = (int)header.SizeOfImage;
                    return true;
                }
            }
            catch
            {
                timestamp = 0;
                filesize = 0;
                return false;
            }
        }

        /// <summary>
        /// Whether this object has been disposed.
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Gets the File Version Information that is stored as a resource in the PE file.  (This is what the
        /// version tab a file's property page is populated with).
        /// </summary>
        public FileVersionInfo GetFileVersionInfo()
        {
            ResourceNode resources = GetResources();
            ResourceNode versionNode = ResourceNode.GetChild(ResourceNode.GetChild(resources, "Version"), "1");
            if (versionNode == null)
                return null;

            if (!versionNode.IsLeaf && versionNode.Children.Count == 1)
                versionNode = versionNode.Children[0];

            PEBuffer buff = AllocBuff();
            byte* bytes = versionNode.FetchData(0, versionNode.DataLength, buff);
            FileVersionInfo ret = new FileVersionInfo(bytes, versionNode.DataLength);

            FreeBuff(buff);
            return ret;
        }

        /// <summary>
        /// For side by side dlls, the manifest that decribes the binding information is stored as the RT_MANIFEST resource, and it
        /// is an XML string.   This routine returns this.
        /// </summary>
        /// <returns></returns>
        public string GetSxSManfest()
        {
            ResourceNode resources = GetResources();
            ResourceNode manifest = ResourceNode.GetChild(ResourceNode.GetChild(resources, "RT_MANIFEST"), "1");
            if (manifest == null)
                return null;

            if (!manifest.IsLeaf && manifest.Children.Count == 1)
                manifest = manifest.Children[0];

            PEBuffer buff = AllocBuff();
            byte* bytes = manifest.FetchData(0, manifest.DataLength, buff);
            string ret = null;
            using (UnmanagedMemoryStream stream = new UnmanagedMemoryStream(bytes, manifest.DataLength))
            using (StreamReader textReader = new StreamReader(stream))
                ret = textReader.ReadToEnd();
            FreeBuff(buff);
            return ret;
        }

        /// <summary>
        /// Closes any file handles and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            // This method can only be called once on a given object.  
            _stream.Dispose();
            _headerBuff.Dispose();
            if (_freeBuff != null)
                _freeBuff.Dispose();

            Disposed = true;
        }

        // TODO make public?
        internal ResourceNode GetResources()
        {
            if (Header.ResourceDirectory.VirtualAddress == 0 || Header.ResourceDirectory.Size < sizeof(IMAGE_RESOURCE_DIRECTORY))
                return null;

            ResourceNode ret = new ResourceNode("", Header.FileOffsetOfResources, this, false, true);
            return ret;
        }

        private PEBuffer _headerBuff;
        private PEBuffer _freeBuff;
        private Stream _stream;
        private bool _virt;

        internal byte* FetchRVA(int rva, int size, PEBuffer buffer)
        {
            int offset = Header.RvaToFileOffset(rva);
            return buffer.Fetch(offset, size);
        }

        internal IntPtr SafeFetchRVA(int rva, int size, PEBuffer buffer)
        {
            return new IntPtr(FetchRVA(rva, size, buffer));
        }

        internal PEBuffer AllocBuff()
        {
            PEBuffer ret = _freeBuff;
            if (ret == null)
                return new PEBuffer(_stream);

            _freeBuff = null;
            return ret;
        }

        internal void FreeBuff(PEBuffer buffer)
        {
            _freeBuff = buffer;
        }
    }
}