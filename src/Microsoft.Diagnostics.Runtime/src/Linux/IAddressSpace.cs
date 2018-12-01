// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal interface IAddressSpace
    {
        int Read(long position, byte[] buffer, int bufferOffset, int count);
        long Length { get; }
        string Name { get; }
    }
}