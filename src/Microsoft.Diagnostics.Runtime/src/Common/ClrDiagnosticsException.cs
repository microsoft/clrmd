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
        internal ClrDiagnosticsException(string message, ClrDiagnosticsExceptionKind kind = ClrDiagnosticsExceptionKind.Unknown, int hr = -2146233088)
            : base(message)
        {
            Kind = kind;
            HResult = hr;
        }

        /// <summary>
        /// Exception kind
        /// </summary>
        public ClrDiagnosticsExceptionKind Kind { get; }

        protected ClrDiagnosticsException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));

            Kind = (ClrDiagnosticsExceptionKind)info.GetValue(nameof(Kind), typeof(ClrDiagnosticsExceptionKind));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));

            info.AddValue(nameof(Kind), Kind, typeof(ClrDiagnosticsExceptionKind));
            base.GetObjectData(info, context);
        }
    }
}