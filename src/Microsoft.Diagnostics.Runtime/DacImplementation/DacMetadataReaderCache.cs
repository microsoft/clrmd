// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacMetadataReaderCache : IDisposable
    {
        private readonly Dictionary<ulong, DacMetadataReader?> _imports = new();
        private readonly SOSDac _sos;

        public DacMetadataReaderCache(SOSDac sos)
        {
            _sos = sos;
        }

        public IAbstractMetadataReader? GetMetadataForModule(ulong moduleAddress)
        {
            lock (_imports)
            {
                if (_imports.TryGetValue(moduleAddress, out DacMetadataReader? reader))
                    return reader;
            }

            MetadataImport? import = _sos.GetMetadataImport(moduleAddress);
            DacMetadataReader? result = import != null ? new(import) : null;
            lock (_imports)
            {
                try
                {
                    _imports.Add(moduleAddress, result);
                    import = null;  // don't dispose
                }
                catch (ArgumentException)
                {
                    result = _imports[moduleAddress];
                }
            }

            // It's ok if a really unexpected exception causes us not to call Dispose.  Eventually
            // this will be cleaned up by the finalizer, but all else being equal we want to
            // opportunistically release memory here.
            import?.Dispose();
            return result;
        }

        public void Dispose()
        {
            lock (_imports)
            {
                foreach (DacMetadataReader? import in _imports.Values)
                    import?.Dispose();

                _imports.Clear();
            }
        }
    }
}