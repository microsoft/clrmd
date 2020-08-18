// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    class NullBinaryLocator : IBinaryLocator
    {
        public string FindBinary(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true)
        {
            return null;
        }

        public string FindBinary(string fileName, ImmutableArray<byte> buildId, bool checkProperties = true)
        {
            return null;
        }

        public Task<string> FindBinaryAsync(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true)
        {
            return Task.FromResult<string>(null);
        }

        public Task<string> FindBinaryAsync(string fileName, ImmutableArray<byte> buildId, bool checkProperties = true)
        {
            return Task.FromResult<string>(null);
        }
    }
}
