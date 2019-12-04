// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A wrapper for exception objects which help with common tasks for exception objects.
    /// Create this using ClrObject.AsException  You may call that when ClrObject.IsException
    /// returns true.
    /// </summary>
    public struct ClrException
    {
        private readonly IExceptionHelpers _helpers;
        private readonly ClrObject _object;

        /// <summary>
        /// The original thread this exception was thrown from.  This may be null if we do not know.
        /// </summary>
        public ClrThread Thread { get; }

        public ClrException(IExceptionHelpers helpers, ClrThread thread, ClrObject obj)
        {
            if (obj.IsNull)
                throw new InvalidOperationException($"Cannot construct a ClrException from a null object.");

            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
            _object = obj;
            Thread = thread;

            Debug.Assert(obj.IsException);
        }

        /// <summary>
        /// Returns the address of the exception object.
        /// </summary>
        public ulong Address => _object;

        /// <summary>
        /// Returns the ClrType for this exception object.
        /// </summary>
        public ClrType Type => _object.Type;

        /// <summary>
        /// Returns the exception message.
        /// </summary>
        public string Message => _object.GetStringField("_message");

        /// <summary>
        /// Returns the inner exception, if one exists, null otherwise.
        /// </summary>
        public ClrException? Inner
        {
            get
            {
                ClrObject obj = _object.GetObjectField("_innerException");
                if (obj.IsNull)
                    return null;

                return obj.AsException();
            }
        }

        /// <summary>
        /// Returns the HRESULT associated with this exception (or S_OK if there isn't one).
        /// </summary>
        public int HResult => _object.GetField<int>("_HResult");

        /// <summary>
        /// Returns the StackTrace for this exception.  Note that this may be empty or partial depending
        /// on the state of the exception in the process.  (It may have never been thrown or we may be in
        /// the middle of constructing the stackwalk.)  This returns an empty list if no stack trace is
        /// associated with this exception object.
        /// </summary>
        public IReadOnlyList<ClrStackFrame> StackTrace => _helpers.GetExceptionStackTrace(Thread, _object);
    }
}