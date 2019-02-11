// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A data reader interface that provides the ProcessId of the DataTarget.
    /// </summary>
    public interface IDataReader2 : IDataReader
    {
        /// <summary>
        /// The ProcessId of the DataTarget.
        /// </summary>
        uint ProcessId { get; }
    }
}
