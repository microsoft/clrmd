// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A wrapper class for exception objects which help with common tasks for exception objects.
    /// Create this using GCHeap.GetExceptionObject.  You may call that when ClrType.IsException
    /// returns true.
    /// </summary>
    public abstract class ClrException
    {
        /// <summary>
        /// Returns the ClrType for this exception object.
        /// </summary>
        public abstract ClrType Type { get; }

        /// <summary>
        /// Returns the exception message.
        /// </summary>
        public abstract string Message { get; }

        /// <summary>
        /// Returns the address of the exception object.
        /// </summary>
        public abstract ulong Address { get; }

        /// <summary>
        /// Returns the inner exception, if one exists, null otherwise.
        /// </summary>
        public abstract ClrException Inner { get; }

        /// <summary>
        /// Returns the HRESULT associated with this exception (or S_OK if there isn't one).
        /// </summary>
        public abstract int HResult { get; }

        /// <summary>
        /// Returns the StackTrace for this exception.  Note that this may be empty or partial depending
        /// on the state of the exception in the process.  (It may have never been thrown or we may be in
        /// the middle of constructing the stackwalk.)  This returns an empty list if no stack trace is
        /// associated with this exception object.
        /// </summary>
        public abstract IList<ClrStackFrame> StackTrace { get; }
    }
}