// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    internal sealed class NullBinaryLocator : IFileLocator
    {
        public NullBinaryLocator()
        {
        }

        public string FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            return null;
        }

        public string FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties)
        {
            return null;
        }
    }
}
