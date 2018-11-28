// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class ErrorType : BaseDesktopHeapType
    {
        public ErrorType(DesktopGCHeap heap)
            : base(0, heap, heap.DesktopRuntime.ErrorModule, 0)
        {
        }

        public override int BaseSize
        {
            get
            {
                return 0;
            }
        }

        public override ClrType BaseType
        {
            get
            {
                return DesktopHeap.ObjectType;
            }
        }

        public override int ElementSize
        {
            get
            {
                return 0;
            }
        }

        public override ClrHeap Heap
        {
            get
            {
                return DesktopHeap;
            }
        }

        public override IList<ClrInterface> Interfaces
        {
            get
            {
                return new ClrInterface[0];
            }
        }

        public override bool IsAbstract
        {
            get
            {
                return false;
            }
        }

        public override bool IsFinalizable
        {
            get
            {
                return false;
            }
        }

        public override bool IsInterface
        {
            get
            {
                return false;
            }
        }

        public override bool IsInternal
        {
            get
            {
                return false;
            }
        }

        public override bool IsPrivate
        {
            get
            {
                return false;
            }
        }

        public override bool IsProtected
        {
            get
            {
                return false;
            }
        }

        public override bool IsPublic
        {
            get
            {
                return false;
            }
        }

        public override bool IsSealed
        {
            get
            {
                return false;
            }
        }

        public override uint MetadataToken
        {
            get
            {
                return 0;
            }
        }

        public override ulong MethodTable
        {
            get
            {
                return 0;
            }
        }

        public override string Name
        {
            get
            {
                return "ERROR";
            }
        }

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
