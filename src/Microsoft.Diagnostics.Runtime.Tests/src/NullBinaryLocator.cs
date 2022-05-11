// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    class NullBinaryLocator : IFileLocator
    {
        public string FindElfImage(string fileName, string archivedUnder, ImmutableArray<byte> buildId, bool checkProperties)
        {
            return null;
        }

        public string FindMachOImage(string fileName, string archivedUnder, ImmutableArray<byte> uuid, bool checkProperties)
        {
            return null;
        }

        public string FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            return null;
        }

        public string FindPEImage(string fileName, string archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties)
        {
            return null;
        }
    }
}
