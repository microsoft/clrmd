// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class ErrorModule : DesktopBaseModule
    {
        private static uint s_id = 0;
        private uint _id = s_id++;

        public ErrorModule(DesktopRuntimeBase runtime)
            : base(runtime)
        {
        }

        public override PdbInfo Pdb
        {
            get
            {
                return null;
            }
        }

        public override IList<ClrAppDomain> AppDomains
        {
            get
            {
                return new ClrAppDomain[0];
            }
        }

        public override string AssemblyName
        {
            get { return "<error>"; }
        }

        public override string Name
        {
            get { return "<error>"; }
        }

        public override bool IsDynamic
        {
            get { return false; }
        }

        public override bool IsFile
        {
            get { return false; }
        }

        public override string FileName
        {
            get { return "<error>"; }
        }

        public override ulong ImageBase
        {
            get { return 0; }
        }

        public override ulong Size
        {
            get { return 0; }
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            return new ClrType[0];
        }

        public override ulong MetadataAddress
        {
            get { return 0; }
        }

        public override ulong MetadataLength
        {
            get { return 0; }
        }

        public override object MetadataImport
        {
            get { return null; }
        }

        internal override ulong GetDomainModule(ClrAppDomain appDomain)
        {
            return 0;
        }

        public override DebuggableAttribute.DebuggingModes DebuggingMode
        {
            get { return DebuggableAttribute.DebuggingModes.None; }
        }

        public override ClrType GetTypeByName(string name)
        {
            return null;
        }

        public override ulong AssemblyId
        {
            get { return _id; }
        }
    }
}
