// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopAppDomain : ClrAppDomain
    {
        public override ClrRuntime Runtime => _runtime;

        /// <summary>
        /// ulong of the AppDomain.
        /// </summary>
        public override ulong Address => _address;

        /// <summary>
        /// The AppDomain's ID.
        /// </summary>
        public override int Id { get; }

        /// <summary>
        /// The name of the AppDomain, as specified when the domain was created.
        /// </summary>
        public override string Name { get; }
        public override IList<ClrModule> Modules => _modules;

        internal int InternalId { get; }

        public override string ConfigurationFile => _runtime.GetConfigFile(_address);

        public override string ApplicationBase
        {
            get
            {
                string appBase = _runtime.GetAppBase(_address);
                if (string.IsNullOrEmpty(appBase))
                    return null;

                Uri uri = new Uri(appBase);
                try
                {
                    return uri.AbsolutePath.Replace('/', '\\');
                }
                catch (InvalidOperationException)
                {
                    return appBase;
                }
            }
        }

        internal DesktopAppDomain(DesktopRuntimeBase runtime, IAppDomainData data, string name)
        {
            _address = data.Address;
            Id = data.Id;
            Name = name;
            InternalId = s_internalId++;
            _runtime = runtime;
        }

        internal void AddModule(ClrModule module)
        {
            _modules.Add(module);
        }

        private readonly ulong _address;
        private readonly List<ClrModule> _modules = new List<ClrModule>();
        private readonly DesktopRuntimeBase _runtime;

        private static int s_internalId;
    }
}