// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal abstract class BaseDesktopHeapType : ClrType
    {
        protected ClrElementType _elementType;
        protected uint _token;
        private IList<ClrInterface> _interfaces;
        private readonly Lazy<GCDesc> _gcDesc;
        protected ulong _constructedMT;

        protected internal override GCDesc GCDesc => _gcDesc.Value;

        public bool Shared { get; internal set; }

        public BaseDesktopHeapType(ulong mt, DesktopGCHeap heap, DesktopBaseModule module, uint token)
        {
            _constructedMT = mt;
            DesktopHeap = heap;
            DesktopModule = module;
            _token = token;
            _gcDesc = new Lazy<GCDesc>(FillGCDesc);
        }

        private GCDesc FillGCDesc()
        {
            DesktopRuntimeBase runtime = DesktopHeap.DesktopRuntime;

            Debug.Assert(_constructedMT != 0, "Attempted to fill GC desc with a constructed (not real) type.");
            if (!runtime.ReadDword(_constructedMT - (ulong)IntPtr.Size, out int entries))
                return null;

            // Get entries in map
            if (entries < 0)
                entries = -entries;

            int slots = 1 + entries * 2;
            byte[] buffer = new byte[slots * IntPtr.Size];
            if (!runtime.ReadMemory(_constructedMT - (ulong)(slots * IntPtr.Size), buffer, buffer.Length, out int read) || read != buffer.Length)
                return null;

            // Construct the gc desc
            return new GCDesc(buffer);
        }

        internal abstract ulong GetModuleAddress(ClrAppDomain domain);

        internal override ClrMethod GetMethod(uint token)
        {
            return null;
        }

        internal DesktopGCHeap DesktopHeap { get; set; }
        internal DesktopBaseModule DesktopModule { get; set; }

        public override ClrElementType ElementType
        {
            get => _elementType;
            internal set => _elementType = value;
        }

        public override uint MetadataToken => _token;

        public override IList<ClrInterface> Interfaces
        {
            get
            {
                if (_interfaces == null)
                    InitInterfaces();

                Debug.Assert(_interfaces != null);
                return _interfaces;
            }
        }

        public List<ClrInterface> InitInterfaces()
        {
            if (DesktopModule == null)
            {
                _interfaces = DesktopHeap.EmptyInterfaceList;
                return null;
            }

            BaseDesktopHeapType baseType = BaseType as BaseDesktopHeapType;
            List<ClrInterface> interfaces = baseType != null ? new List<ClrInterface>(baseType.Interfaces) : null;
            MetaDataImport import = DesktopModule.GetMetadataImport();
            if (import == null)
            {
                _interfaces = DesktopHeap.EmptyInterfaceList;
                return null;
            }

            foreach (int token in import.EnumerateInterfaceImpls((int)_token))
            {
                if (import.GetInterfaceImplProps(token, out int mdClass, out int mdIFace))
                {
                    if (interfaces == null)
                        interfaces = new List<ClrInterface>();

                    ClrInterface result = GetInterface(import, mdIFace);
                    if (result != null && !interfaces.Contains(result))
                        interfaces.Add(result);
                }
            }

            if (interfaces == null)
                _interfaces = DesktopHeap.EmptyInterfaceList;
            else
                _interfaces = interfaces.ToArray();

            return interfaces;
        }

        private ClrInterface GetInterface(MetaDataImport import, int mdIFace)
        {
            ClrInterface result = null;
            if (!import.GetTypeDefProperties(mdIFace, out string name, out TypeAttributes attrs, out int extends))
            {
                name = import.GetTypeRefName(mdIFace);
            }

            // TODO:  Handle typespec case.
            if (name != null && !DesktopHeap.Interfaces.TryGetValue(name, out result))
            {
                ClrInterface type = null;
                if (extends != 0 && extends != 0x01000000)
                    type = GetInterface(import, extends);

                result = new DesktopHeapInterface(name, type);
                DesktopHeap.Interfaces[name] = result;
            }

            return result;
        }
    }
}