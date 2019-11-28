// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    // Todo:  Convert to struct, ClrObject.AsException();
    internal class DesktopException : ClrException
    {
        public DesktopException(ClrObject obj)
        {
            _object = obj;
        }

        public override ClrType Type => _object.Type;

        public override string Message => _object.GetStringField("_message");
        public override ulong Address => _object;

        public override ClrException Inner
        {
            get
            {
                ClrObject inner = _object.GetObjectField("_innerException");
                if (inner.IsNull)
                    return null;

                return new DesktopException(inner);
            }
        }

        public override IReadOnlyList<ClrStackFrame> StackTrace
        {
            get
            {
                if (_stackTrace == null)
                    _stackTrace = _type.DesktopHeap.DesktopRuntime.GetExceptionStackTrace(_object, _type);

                return _stackTrace;
            }
        }

        public override int HResult
        {
            get
            {
                ClrInstanceField field = _type.GetFieldByName("_HResult");
                if (field != null)
                    return (int)field.GetValue(_object);

                ClrRuntimeImpl runtime = _type.DesktopHeap.DesktopRuntime;
                uint offset = runtime.GetExceptionHROffset();
                runtime.ReadPrimitive(_object + offset, out int hr);

                return hr;
            }
        }

        private readonly ClrObject _object;
        private IList<ClrStackFrame> _stackTrace;
    }
}