// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.Utilities;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class PrimitiveType : BaseDesktopHeapType
    {
        public PrimitiveType(DesktopGCHeap heap, ClrElementType type)
            : base(0, heap, heap.DesktopRuntime.ErrorModule, 0)
        {
            ElementType = type;
        }

        public override int BaseSize
        {
            get
            {
                return DesktopInstanceField.GetSize(this, ElementType);
            }
        }

        public override ClrType BaseType
        {
            get
            {
                return DesktopHeap.ValueType;
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
                return GetElementTypeName();
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

        private string GetElementTypeName()
        {
            switch (ElementType)
            {
                case ClrElementType.Boolean:
                    return "System.Boolean";

                case ClrElementType.Char:
                    return "System.Char";

                case ClrElementType.Int8:
                    return "System.SByte";

                case ClrElementType.UInt8:
                    return "System.Byte";

                case ClrElementType.Int16:
                    return "System.Int16";

                case ClrElementType.UInt16:
                    return "System.UInt16";

                case ClrElementType.Int32:
                    return "System.Int32";

                case ClrElementType.UInt32:
                    return "System.UInt32";

                case ClrElementType.Int64:
                    return "System.Int64";

                case ClrElementType.UInt64:
                    return "System.UInt64";

                case ClrElementType.Float:
                    return "System.Single";

                case ClrElementType.Double:
                    return "System.Double";

                case ClrElementType.NativeInt:
                    return "System.IntPtr";

                case ClrElementType.NativeUInt:
                    return "System.UIntPtr";

                case ClrElementType.Struct:
                    return "Sytem.ValueType";
            }

            return ElementType.ToString();
        }
    }
}
