// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopException : ClrException
    {
        public DesktopException(ulong objRef, BaseDesktopHeapType type)
        {
            _object = objRef;
            _type = type;
        }

        public override ClrType Type => _type;

        public override string Message
        {
            get
            {
                ClrInstanceField field = _type.GetFieldByName("_message");
                if (field != null)
                    return (string)field.GetValue(_object);

                DesktopRuntimeBase runtime = _type.DesktopHeap.DesktopRuntime;
                uint offset = runtime.GetExceptionMessageOffset();
                Debug.Assert(offset > 0);

                ulong message = _object + offset;
                if (!runtime.ReadPointer(message, out message))
                    return null;

                return _type.DesktopHeap.GetStringContents(message);
            }
        }

        public override ulong Address => _object;

        public override ClrException Inner
        {
            get
            {
                // TODO:  This needs to get the field offset by runtime instead.
                ClrInstanceField field = _type.GetFieldByName("_innerException");
                if (field == null)
                    return null;

                object inner = field.GetValue(_object);
                if (inner == null || !(inner is ulong) || (ulong)inner == 0)
                    return null;

                ulong ex = (ulong)inner;
                BaseDesktopHeapType type = (BaseDesktopHeapType)_type.DesktopHeap.GetObjectType(ex);

                return new DesktopException(ex, type);
            }
        }

        public override IList<ClrStackFrame> StackTrace
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

                DesktopRuntimeBase runtime = _type.DesktopHeap.DesktopRuntime;
                uint offset = runtime.GetExceptionHROffset();
                runtime.ReadDword(_object + offset, out int hr);

                return hr;
            }
        }

        private readonly ulong _object;
        private readonly BaseDesktopHeapType _type;
        private IList<ClrStackFrame> _stackTrace;
    }
}