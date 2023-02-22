﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal interface IModuleData
    {
        IModuleHelpers Helpers { get; }

        ulong Address { get; }
        bool IsPEFile { get; }
        ulong PEImageBase { get; }
        ulong ILImageBase { get; }
        bool IsFlatLayout { get; }
        bool IsDynamic { get; }
        ulong Size { get; }
        ulong MetadataStart { get; }
        string? Name { get; }
        string? FileName { get; }
        string? SimpleName { get; }
        string? AssemblyName { get; }
        ulong MetadataLength { get; }
        bool IsReflection { get; }
        ulong AssemblyAddress { get; }
        ulong ThunkHeap { get; }
        ulong LoaderAllocator { get; }
    }
}