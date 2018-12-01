// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// FileVersionInfo reprents the extended version formation that is optionally placed in the PE file resource area.
    /// </summary>
    public sealed unsafe class FileVersionInfo
    {
        // TODO incomplete, but this is all I need.  
        /// <summary>
        /// The verison string
        /// </summary>
        public string FileVersion { get; }

        /// <summary>
        /// Comments to supplement the file version
        /// </summary>
        public string Comments { get; }

        internal FileVersionInfo(byte* data, int dataLen)
        {
            FileVersion = "";
            if (dataLen <= 0x5c)
                return;

            // See http://msdn.microsoft.com/en-us/library/ms647001(v=VS.85).aspx
            byte* stringInfoPtr = data + 0x5c; // Gets to first StringInfo

            // TODO search for FileVersion string ... 
            string dataAsString = new string((char*)stringInfoPtr, 0, (dataLen - 0x5c) / 2);

            FileVersion = GetDataString(dataAsString, "FileVersion");
            Comments = GetDataString(dataAsString, "Comments");
        }

        private static string GetDataString(string dataAsString, string fileVersionKey)
        {
            int fileVersionIdx = dataAsString.IndexOf(fileVersionKey);
            if (fileVersionIdx >= 0)
            {
                int valIdx = fileVersionIdx + fileVersionKey.Length;
                for (;;)
                {
                    valIdx++;
                    if (valIdx >= dataAsString.Length)
                        return null;

                    if (dataAsString[valIdx] != (char)0)
                        break;
                }

                int varEndIdx = dataAsString.IndexOf((char)0, valIdx);
                if (varEndIdx < 0)
                    return null;

                return dataAsString.Substring(valIdx, varEndIdx - valIdx);
            }

            return null;
        }
    }
}