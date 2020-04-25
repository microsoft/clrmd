// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class DefaultDataReader : CustomDataReader
    {
        public override IDataReader DataReader { get; }

        public override CacheOptions? CacheOptions => null;

        public override IBinaryLocator? BinaryLocator => null;  // use default locator

        public override string? DefaultSymbolPath => null;

        public DefaultDataReader(IDataReader reader)
        {
            DataReader = reader;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (DataReader is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }
}
