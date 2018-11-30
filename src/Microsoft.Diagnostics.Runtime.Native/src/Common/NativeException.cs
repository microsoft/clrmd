// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Native
{
    internal class NativeException : ClrException
    {
        private readonly ulong _hResult;

        private ClrException _innerException;

        public NativeException(
            ClrType clrType,
            ulong ccwPtr,
            ulong hResult,
            ulong threadId,
            ulong exceptionId,
            ulong innerExceptionId,
            ulong nestingLevel,
            IList<ClrStackFrame> stackFrames)
        {
            Type = clrType;
            Address = ccwPtr;
            _hResult = hResult;
            ThreadId = threadId;
            ExceptionId = exceptionId;
            InnerExceptionId = innerExceptionId;
            NestingLevel = nestingLevel;
            StackTrace = stackFrames;
        }

        public override ulong Address { get; }
        public override int HResult => (int)_hResult;
        public override ClrType Type { get; }
        public override string Message => null;
        public override ClrException Inner => _innerException;

        internal void SetInnerException(ClrException inner)
        {
            _innerException = inner;
        }

        public override IList<ClrStackFrame> StackTrace { get; }
        public ulong InnerExceptionId { get; }
        public ulong ThreadId { get; }
        public ulong NestingLevel { get; }
        public ulong ExceptionId { get; }
    }
}