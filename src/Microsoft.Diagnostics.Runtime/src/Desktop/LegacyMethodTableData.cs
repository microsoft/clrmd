// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct LegacyMethodTableData : IMethodTableData
    {
        public readonly uint IsFree; // everything else is NULL if this is true.
        public readonly ulong EEClass;
        public readonly ulong ParentMethodTable;
        public readonly ushort NumInterfaces;
        public readonly ushort NumVtableSlots;
        public readonly uint BaseSize;
        public readonly uint ComponentSize;
        public readonly uint IsShared; // flags & enum_flag_DomainNeutral
        public readonly uint SizeofMethodTable;
        public readonly uint IsDynamic;
        public readonly uint ContainsPointers;

        bool IMethodTableData.ContainsPointers => ContainsPointers != 0;
        uint IMethodTableData.BaseSize => BaseSize;
        uint IMethodTableData.ComponentSize => ComponentSize;
        ulong IMethodTableData.EEClass => EEClass;
        bool IMethodTableData.Free => IsFree != 0;
        ulong IMethodTableData.Parent => ParentMethodTable;
        bool IMethodTableData.Shared => IsShared != 0;
        uint IMethodTableData.NumMethods => NumVtableSlots;
        ulong IMethodTableData.ElementTypeHandle => throw new NotImplementedException();
        uint IMethodTableData.Token => 0;
        ulong IMethodTableData.Module => 0;
    }
}