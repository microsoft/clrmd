// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V2EEClassData : IEEClassData, IFieldInfo
    {
        public readonly ulong MethodTable;
        public readonly ulong Module;
        public readonly short NumVTableSlots;
        public readonly short NumMethodSlots;
        public readonly short NumInstanceFields;
        public readonly short NumStaticFields;
        public readonly uint ClassDomainNeutralIndex;
        public readonly uint AttrClass; // cached metadata
        public readonly uint Token; // Metadata token

        public readonly ulong AddrFirstField; // If non-null, you can retrieve more

        public readonly short ThreadStaticOffset;
        public readonly short ThreadStaticsSize;
        public readonly short ContextStaticOffset;
        public readonly short ContextStaticsSize;

        ulong IEEClassData.Module => Module;
        ulong IEEClassData.MethodTable => MethodTable;
        
        uint IFieldInfo.InstanceFields => (uint)NumInstanceFields;
        uint IFieldInfo.StaticFields => (uint)NumStaticFields;
        uint IFieldInfo.ThreadStaticFields => 0;
        ulong IFieldInfo.FirstField => AddrFirstField;
    }
}