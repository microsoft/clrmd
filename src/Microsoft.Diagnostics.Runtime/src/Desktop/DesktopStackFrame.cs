// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.ICorDebug;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopStackFrame : ClrStackFrame
    {
        public override ClrThread Thread
        {
            get
            {
                return _thread;
            }
        }

        public override ulong StackPointer
        {
            get { return _sp; }
        }

        public override ulong InstructionPointer
        {
            get { return _ip; }
        }

        public override ClrStackFrameType Kind
        {
            get { return _type; }
        }

        public override string DisplayString
        {
            get { return _frameName; }
        }

        public override ClrMethod Method
        {
            get
            {
                if (_method == null && _ip != 0 && _type == ClrStackFrameType.ManagedMethod)
                    _method = _runtime.GetMethodByAddress(_ip);

                return _method;
            }
        }

        public ICorDebugILFrame CordbFrame { get; internal set; }

        public override string ToString()
        {
            if (_type == ClrStackFrameType.ManagedMethod)
                return _frameName;

            int methodLen = 0;
            int methodTypeLen = 0;

            if (_method != null)
            {
                methodLen = _method.Name.Length;
                if (_method.Type != null)
                    methodTypeLen = _method.Type.Name.Length;
            }

            StringBuilder sb = new StringBuilder(_frameName.Length + methodLen + methodTypeLen + 10);

            sb.Append('[');
            sb.Append(_frameName);
            sb.Append(']');

            if (_method != null)
            {
                sb.Append(" (");

                if (_method.Type != null)
                {
                    sb.Append(_method.Type.Name);
                    sb.Append('.');
                }

                sb.Append(_method.Name);
                sb.Append(')');
            }

            return sb.ToString();
        }

        public DesktopStackFrame(DesktopRuntimeBase runtime, DesktopThread thread, ulong ip, ulong sp, ulong md)
        {
            _runtime = runtime;
            _thread = thread;
            _ip = ip;
            _sp = sp;
            _frameName = _runtime.GetNameForMD(md) ?? "Unknown";
            _type = ClrStackFrameType.ManagedMethod;

            InitMethod(md);
        }

        public DesktopStackFrame(DesktopRuntimeBase runtime, DesktopThread thread, ulong sp, ulong md)
        {
            _runtime = runtime;
            _thread = thread;
            _sp = sp;
            _frameName = _runtime.GetNameForMD(md) ?? "Unknown";
            _type = ClrStackFrameType.Runtime;

            InitMethod(md);
        }

        public DesktopStackFrame(DesktopRuntimeBase runtime, DesktopThread thread, ulong sp, string method, ClrMethod innerMethod)
        {
            _runtime = runtime;
            _thread = thread;
            _sp = sp;
            _frameName = method ?? "Unknown";
            _type = ClrStackFrameType.Runtime;
            _method = innerMethod;
        }

        private void InitMethod(ulong md)
        {
            if (_method != null)
                return;

            if (_ip != 0 && _type == ClrStackFrameType.ManagedMethod)
            {
                _method = _runtime.GetMethodByAddress(_ip);
            }
            else if (md != 0)
            {
                IMethodDescData mdData = _runtime.GetMethodDescData(md);
                _method = DesktopMethod.Create(_runtime, mdData);
            }
        }

        private ulong _ip, _sp;
        private string _frameName;
        private ClrStackFrameType _type;
        private ClrMethod _method;
        private DesktopRuntimeBase _runtime;
        private DesktopThread _thread;
    }
}
