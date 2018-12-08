// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// For backward compatibility only
    /// </summary>
    public static class ClrInfoExtensions
    {
        /// <summary>
        /// Creates a runtime from the given Dac file on disk.
        /// </summary>
        public static ClrRuntime CreateRuntime(this ClrInfo clrInfo)
        {
            if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));

            return clrInfo.DataTarget.CreateRuntime(clrInfo);
        }

        /// <summary>
        /// Creates a runtime from a given IXClrDataProcess interface. Used for debugger plugins.
        /// </summary>
        public static ClrRuntime CreateRuntime(this ClrInfo clrInfo, object clrDataProcess)
        {
            if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));

            return clrInfo.DataTarget.CreateRuntime(clrInfo, clrDataProcess);
        }

        /// <summary>
        /// Creates a runtime from the given Dac file on disk.
        /// </summary>
        public static ClrRuntime CreateRuntime(this ClrInfo clrInfo, string dacFilename, bool ignoreMismatch = false)
        {
            if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));

            return clrInfo.DataTarget.CreateRuntime(clrInfo, dacFilename, ignoreMismatch);
        }
    }
}