// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime.AbstractDac;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacServiceProvider : IServiceProvider
    {
        private readonly ClrInfo _clrInfo;
        private readonly DacLibrary _library;

        private IAbstractRuntime? _runtime;

        public DacServiceProvider(ClrInfo clrInfo, DacLibrary library)
        {
            _clrInfo = clrInfo;
            _library = library;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IAbstractRuntime))
                return _runtime ??= new DacRuntime(_clrInfo, _library);

            return null;
        }
    }
}