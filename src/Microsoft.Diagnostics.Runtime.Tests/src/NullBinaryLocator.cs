// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    internal sealed class NullBinaryLocator : IFileLocator
    {
        private readonly IFileLocator _realLocator;

        public NullBinaryLocator(IFileLocator locator)
        {
            _realLocator = locator;
        }

        public string FindElfImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildId, bool checkProperties)
        {
            if (fileName.Contains("mscordac", StringComparison.OrdinalIgnoreCase))
                return _realLocator.FindElfImage(fileName, archivedUnder, buildId, checkProperties);

            return null;
        }

        public string FindMachOImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> uuid, bool checkProperties)
        {
            if (fileName.Contains("mscordac", StringComparison.OrdinalIgnoreCase))
                return _realLocator.FindMachOImage(fileName, archivedUnder, uuid, checkProperties);

            return null;
        }

        public string FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            if (fileName.Contains("mscordac", StringComparison.OrdinalIgnoreCase))
                return _realLocator.FindPEImage(fileName, buildTimeStamp, imageSize, checkProperties);

            return null;
        }

        public string FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties)
        {
            if (fileName.Contains("mscordac", StringComparison.OrdinalIgnoreCase))
                return _realLocator.FindPEImage(fileName, archivedUnder, buildIdOrUUID, originalPlatform, checkProperties);

            return null;
        }
    }
}
