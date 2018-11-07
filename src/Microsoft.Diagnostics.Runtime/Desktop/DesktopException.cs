// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        public override ClrType Type
        {
            get { return _type; }
        }

        public override string Message
        {
            get
            {
                var field = _type.GetFieldByName("_message");
                if (field != null)
                    return (string)field.GetValue(_object);

                var runtime = _type.DesktopHeap.DesktopRuntime;
                uint offset = runtime.GetExceptionMessageOffset();
                Debug.Assert(offset > 0);

                ulong message = _object + offset;
                if (!runtime.ReadPointer(message, out message))
                    return null;

                return _type.DesktopHeap.GetStringContents(message);
            }
        }

        public override ulong Address
        {
            get { return _object; }
        }

        public override ClrException Inner
        {
            get
            {
                // TODO:  This needs to get the field offset by runtime instead.
                var field = _type.GetFieldByName("_innerException");
                if (field == null)
                    return null;
                object inner = field.GetValue(_object);
                if (inner == null || !(inner is ulong) || ((ulong)inner == 0))
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
                var field = _type.GetFieldByName("_HResult");
                if (field != null)
                    return (int)field.GetValue(_object);

                var runtime = _type.DesktopHeap.DesktopRuntime;
                uint offset = runtime.GetExceptionHROffset();
                runtime.ReadDword(_object + offset, out int hr);

                return hr;
            }
        }

        #region Private
        private ulong _object;
        private BaseDesktopHeapType _type;
        private IList<ClrStackFrame> _stackTrace;
        #endregion
    }
}
