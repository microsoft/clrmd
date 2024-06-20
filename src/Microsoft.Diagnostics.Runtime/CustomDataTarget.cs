// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Azure.Core;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A user-defined data reader.
    /// Note that this class will be kept alive by <see cref="DataTarget"/> until <see cref="DataTarget.Dispose"/>
    /// is called.
    /// </summary>
    public class CustomDataTarget : IDisposable
    {
        /// <summary>
        /// The environment variable to set to true if.
        /// </summary>
        public const string TraceSymbolsEnvVariable = "ClrMD_TraceSymbolRequests";

        /// <summary>
        /// The data reader that ClrMD will use to read data from the target.
        /// </summary>
        public IDataReader DataReader { get; set; }

        /// <summary>
        ///  An optional set of cache options.  Returning null from this property will use ClrMD's default
        ///  cache options.
        /// </summary>
        public CacheOptions? CacheOptions { get; set; }

        /// <summary>
        /// An optional file locator.  Returning null from this property will use ClrMD's file binary
        /// locator, which uses either <see cref="DefaultSymbolPath"/> (if non null) or the _NT_SYMBOL_PATH (if
        /// <see cref="DefaultSymbolPath"/> is null) environment variable to search for missing binaries.
        /// </summary>
        public IFileLocator? FileLocator { get; set; }

        /// <summary>
        /// If <see cref="FileLocator"/> is null, this path will be used as the symbol path for the default
        /// binary locator.  This property has no effect if <see cref="FileLocator"/> is non-null.
        /// </summary>
        public string? DefaultSymbolPath { get; set; }

        /// <summary>
        /// If true, all runtimes in the target process will be enumerated.
        /// </summary>
        public bool ForceCompleteRuntimeEnumeration { get; set; }

        public TokenCredential? SymbolTokenCredential { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="reader">A non-null IDataReader.</param>
        /// <param name="symbolCredential">The TokenCredential to use for any Azure based symbol servers (set to null if not using one).</param>
        public CustomDataTarget(IDataReader reader, TokenCredential? symbolCredential = null)
        {
            DataReader = reader ?? throw new ArgumentNullException(nameof(reader));
            SymbolTokenCredential = symbolCredential;
        }

        /// <summary>
        /// Dispose method.  Called when <see cref="DataTarget.Dispose"/> is called.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        ~CustomDataTarget() => Dispose(disposing: false);

        /// <summary>
        /// Dispose implementation.  The default implementation will call Dispose() on DataReader if
        /// it implements IDisposable.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (DataReader is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        internal static bool GetTraceEnvironmentVariable()
        {
            string? value = Environment.GetEnvironmentVariable(TraceSymbolsEnvVariable);
            if (value is null)
                return false;

            if (bool.TryParse(value, out bool result))
                return result;

            return value.Equals("1", StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString() => DataReader?.DisplayName ?? GetType().Name;
    }
}