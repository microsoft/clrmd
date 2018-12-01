// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FieldData : IFieldData
    {
        public readonly uint ElementType; // CorElementType
        public readonly uint SigType; // CorElementType
        public readonly ulong TypeMethodTable; // NULL if Type is not loaded
        public readonly ulong TypeModule;
        public readonly uint MDType;
        public readonly uint MDField;
        public readonly ulong MTOfEnclosingClass;
        public readonly uint Offset;
        public readonly uint IsThreadLocal;
        public readonly uint IsContextLocal;
        public readonly uint IsStatic;
        public readonly ulong NextField;

        uint IFieldData.CorElementType => ElementType;
        uint IFieldData.SigType => SigType;
        ulong IFieldData.TypeMethodTable => TypeMethodTable;
        ulong IFieldData.Module => TypeModule;
        uint IFieldData.TypeToken => MDType;
        uint IFieldData.FieldToken => MDField;
        ulong IFieldData.EnclosingMethodTable => MTOfEnclosingClass;
        uint IFieldData.Offset => Offset;
        bool IFieldData.IsThreadLocal => IsThreadLocal != 0;
        bool IFieldData.IsContextLocal => IsContextLocal != 0;
        bool IFieldData.IsStatic => IsStatic != 0;
        ulong IFieldData.NextField => NextField;
    }
}