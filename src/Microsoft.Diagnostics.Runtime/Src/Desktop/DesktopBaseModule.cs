// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal abstract class DesktopBaseModule : ClrModule
    {
        protected DesktopRuntimeBase _runtime;

        public override ClrRuntime Runtime
        {
            get
            {
                return _runtime;
            }
        }

        internal abstract ulong GetDomainModule(ClrAppDomain appDomain);

        internal ulong ModuleId { get; set; }

        internal virtual MetaDataImport GetMetadataImport()
        {
            return null;
        }

        public int Revision { get; set; }

        public DesktopBaseModule(DesktopRuntimeBase runtime)
        {
            _runtime = runtime;
        }
    }
}
