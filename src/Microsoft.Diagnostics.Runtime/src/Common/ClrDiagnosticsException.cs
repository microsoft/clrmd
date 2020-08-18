// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Exception thrown by Microsoft.Diagnostics.Runtime unless there is a more appropriate
    /// exception subclass.
    /// </summary>
    [Serializable]
    public class ClrDiagnosticsException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public ClrDiagnosticsException()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClrDiagnosticsException(string message) : base(message)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClrDiagnosticsException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClrDiagnosticsException(string message, int hr)
            : base(message)
        {
            HResult = hr;
        }

        protected ClrDiagnosticsException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            base.GetObjectData(info, context);
        }
    }
}