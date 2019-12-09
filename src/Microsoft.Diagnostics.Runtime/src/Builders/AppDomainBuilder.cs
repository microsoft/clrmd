// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal class AppDomainBuilder : IAppDomainData
    {
        private readonly SOSDac _sos;
        private readonly AppDomainStoreData _appDomainStore;
        private AppDomainData _appDomainData;

        public ulong SharedDomain => _appDomainStore.SharedDomain;
        public ulong SystemDomain => _appDomainStore.SystemDomain;
        public int AppDomainCount => _appDomainStore.AppDomainCount;

        public IAppDomainHelpers Helpers { get; }
        public string? Name
        {
            get
            {
                if (SharedDomain == Address)
                    return "Shared Domain";

                if (SystemDomain == Address)
                    return "System Domain";

                return _sos.GetAppDomainName(Address);
            }
        }

        public int Id => _appDomainData.Id;
        public ulong Address { get; private set; }

        public AppDomainBuilder(SOSDac sos, IAppDomainHelpers helpers)
        {
            _sos = sos;
            Helpers = helpers;

            _sos.GetAppDomainStoreData(out _appDomainStore);
        }

        public bool Init(ulong address)
        {
            Address = address;
            return _sos.GetAppDomainData(Address, out _appDomainData);
        }
    }
}
