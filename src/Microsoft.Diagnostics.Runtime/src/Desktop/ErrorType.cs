// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class ErrorType : BaseDesktopHeapType
    {
        public ErrorType(DesktopGCHeap heap)
            : base(0, heap, heap.DesktopRuntime.ErrorModule, 0)
        {
        }

        public override int BaseSize => 0;
        public override ClrType BaseType => DesktopHeap.ObjectType;
        public override int ElementSize => 0;
        public override ClrHeap Heap => DesktopHeap;
        public override IList<ClrInterface> Interfaces => new ClrInterface[0];
        public override bool IsAbstract => false;
        public override bool IsFinalizable => false;
        public override bool IsInterface => false;
        public override bool IsInternal => false;
        public override bool IsPrivate => false;
        public override bool IsProtected => false;
        public override bool IsPublic => false;
        public override bool IsSealed => false;
        public override uint MetadataToken => 0;
        public override ulong MethodTable => 0;
        public override string Name => "ERROR";

        public override IEnumerable<ulong> EnumerateMethodTables()
        {
            return new ulong[0];
        }

        public override void EnumerateRefsOfObject(ulong objRef, Action<ulong, int> action)
        {
        }

        public override void EnumerateRefsOfObjectCarefully(ulong objRef, Action<ulong, int> action)
        {
        }

        public override ulong GetArrayElementAddress(ulong objRef, int index)
        {
            throw new InvalidOperationException();
        }

        public override object GetArrayElementValue(ulong objRef, int index)
        {
            throw new InvalidOperationException();
        }

        public override int GetArrayLength(ulong objRef)
        {
            throw new InvalidOperationException();
        }

        public override ClrInstanceField GetFieldByName(string name)
        {
            return null;
        }

        public override bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset)
        {
            childField = null;
            childFieldOffset = 0;
            return false;
        }

        public override ulong GetSize(ulong objRef)
        {
            return 0;
        }

        public override ClrStaticField GetStaticFieldByName(string name)
        {
            return null;
        }

        internal override ulong GetModuleAddress(ClrAppDomain domain)
        {
            return 0;
        }

        public override IList<ClrInstanceField> Fields => new ClrInstanceField[0];
    }
}