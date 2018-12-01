// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal struct IMAGE_DEBUG_DIRECTORY
    {
        public int Characteristics;
        public int TimeDateStamp;
        public short MajorVersion;
        public short MinorVersion;
        public IMAGE_DEBUG_TYPE Type;
        public int SizeOfData;
        public int AddressOfRawData;
        public int PointerToRawData;
    }
}