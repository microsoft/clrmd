// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal struct FileEntry : IEquatable<FileEntry>
    {
        public string FileName;
        public int TimeStamp;
        public int FileSize;

        public FileEntry(string filename, int timestamp, int filesize)
        {
            FileName = filename;
            TimeStamp = timestamp;
            FileSize = filesize;
        }

        public override int GetHashCode()
        {
            return FileName.ToUpperInvariant().GetHashCode() ^ TimeStamp ^ FileSize;
        }

        public override bool Equals(object? obj)
        {
            return obj is FileEntry other && Equals(other);
        }

        public bool Equals(FileEntry other)
        {
            return FileName.Equals(other.FileName, StringComparison.OrdinalIgnoreCase) && TimeStamp == other.TimeStamp && FileSize == other.FileSize;
        }
    }
}