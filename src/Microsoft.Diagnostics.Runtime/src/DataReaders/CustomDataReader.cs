// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A user-defined data reader.
    /// Note that this class will be kept alive by <see cref="DataTarget"/> until <see cref="DataTarget.Dispose"/>
    /// is called.
    /// </summary>
    public abstract class CustomDataReader : IDisposable
    {
        /// <summary>
        /// The data reader that ClrMD will use to read data from the target.
        /// </summary>
        public abstract IDataReader DataReader { get; }

        /// <summary>
        ///  An optional set of cache options.  Returning null from this propery will use ClrMD's default
        ///  cache options.
        /// </summary>
        public abstract CacheOptions? CacheOptions { get; }

        /// <summary>
        /// An optional binary locator.  Returning null from this property will use ClrMD's default binary
        /// locator, which uses either <see cref="DefaultSymbolPath"/> (if non null) or the _NT_SYMBOL_PATH (if
        /// <see cref="DefaultSymbolPath"/> is null) environment variable to search for missing binaries.
        /// </summary>
        public abstract IBinaryLocator? BinaryLocator { get; }

        /// <summary>
        /// If <see cref="BinaryLocator"/> is null, this path will be used as the symbol path for the default
        /// binary locator.  This property has no effect if <see cref="BinaryLocator"/> is non-null.
        /// </summary>
        public abstract string? DefaultSymbolPath { get; }

        /// <summary>
        /// Dispose method.  Called when <see cref="DataTarget.Dispose"/> is called.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        ~CustomDataReader() => Dispose(disposing: false);

        protected abstract void Dispose(bool disposing);

        public override string ToString() => DataReader?.DisplayName ?? GetType().Name;
    }
}
