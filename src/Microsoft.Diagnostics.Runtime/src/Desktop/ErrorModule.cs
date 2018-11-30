// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class ErrorModule : DesktopBaseModule
    {
        private static uint s_id;
        private readonly uint _id = s_id++;

        public ErrorModule(DesktopRuntimeBase runtime)
            : base(runtime)
        {
        }

        public override PdbInfo Pdb => null;
        public override IList<ClrAppDomain> AppDomains => new ClrAppDomain[0];
        public override string AssemblyName => "<error>";
        public override string Name => "<error>";
        public override bool IsDynamic => false;
        public override bool IsFile => false;
        public override string FileName => "<error>";
        public override ulong ImageBase => 0;
        public override ulong Size => 0;

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            return new ClrType[0];
        }

        public override ulong MetadataAddress => 0;
        public override ulong MetadataLength => 0;
        public override object MetadataImport => null;

        internal override ulong GetDomainModule(ClrAppDomain appDomain)
        {
            return 0;
        }

        public override DebuggableAttribute.DebuggingModes DebuggingMode => DebuggableAttribute.DebuggingModes.None;

        public override ClrType GetTypeByName(string name)
        {
            return null;
        }

        public override ulong AssemblyId => _id;
    }
}