// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct LegacyDomainLocalModuleData : IDomainLocalModuleData
    {
        public readonly ulong AppDomainAddr;
        public readonly IntPtr ModuleID;

        public readonly ulong ClassData;
        public readonly ulong DynamicClassTable;
        public readonly ulong GCStaticDataStart;
        public readonly ulong NonGCStaticDataStart;

        ulong IDomainLocalModuleData.AppDomainAddr => AppDomainAddr;
        ulong IDomainLocalModuleData.ModuleID => (ulong)ModuleID.ToInt64();
        ulong IDomainLocalModuleData.ClassData => ClassData;
        ulong IDomainLocalModuleData.DynamicClassTable => DynamicClassTable;
        ulong IDomainLocalModuleData.GCStaticDataStart => GCStaticDataStart;
        ulong IDomainLocalModuleData.NonGCStaticDataStart => NonGCStaticDataStart;
    }
}