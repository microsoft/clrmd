// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// FileVersionInfo reprents the extended version formation that is optionally placed in the PE file resource area.
    /// </summary>
    public sealed unsafe class FileVersionInfo
    {
        /// <summary>
        /// Gets the position of the string data within the resource block.
        /// See http://msdn.microsoft.com/en-us/library/ms647001(v=VS.85).aspx
        /// </summary>
        public const int DataOffset = 0x5c;

        /// <summary>
        /// The verison string
        /// </summary>
        public string? FileVersion { get; }

        /// <summary>
        /// Comments to supplement the file version
        /// </summary>
        public string? Comments { get; }

        internal FileVersionInfo(ReadOnlySpan<byte> data)
        {
            data = data.Slice(DataOffset);

            ReadOnlySpan<char> dataAsString = MemoryMarshal.Cast<byte, char>(data);

            FileVersion = GetDataString(dataAsString, "FileVersion".AsSpan());
            Comments = GetDataString(dataAsString, "Comments".AsSpan());
        }

        private static string? GetDataString(ReadOnlySpan<char> dataAsString, ReadOnlySpan<char> fileVersionKey)
        {
            int fileVersionIndex = dataAsString.IndexOf(fileVersionKey);
            if (fileVersionIndex < 0)
                return null;

            dataAsString = dataAsString.Slice(fileVersionIndex + fileVersionKey.Length);
            dataAsString = dataAsString.TrimStart('\0');

            int endIndex = dataAsString.IndexOf('\0');
            if (endIndex < 0)
                return null;

            return dataAsString.Slice(0, endIndex).ToString();
        }

        public override string? ToString() => FileVersion;
    }
}
