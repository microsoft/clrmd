// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopNativeWorkItem : NativeWorkItem
    {
        private WorkItemKind _kind;
        private ulong _callback, _data;

        public DesktopNativeWorkItem(DacpWorkRequestData result)
        {
            _callback = result.Function;
            _data = result.Context;

            switch (result.FunctionType)
            {
                default:
                case WorkRequestFunctionTypes.UNKNOWNWORKITEM:
                    _kind = WorkItemKind.Unknown;
                    break;

                case WorkRequestFunctionTypes.TIMERDELETEWORKITEM:
                    _kind = WorkItemKind.TimerDelete;
                    break;

                case WorkRequestFunctionTypes.QUEUEUSERWORKITEM:
                    _kind = WorkItemKind.QueueUserWorkItem;
                    break;

                case WorkRequestFunctionTypes.ASYNCTIMERCALLBACKCOMPLETION:
                    _kind = WorkItemKind.AsyncTimer;
                    break;

                case WorkRequestFunctionTypes.ASYNCCALLBACKCOMPLETION:
                    _kind = WorkItemKind.AsyncCallback;
                    break;
            }
        }


        public DesktopNativeWorkItem(WorkRequestData result)
        {
            _callback = result.Function;
            _data = result.Context;
            _kind = WorkItemKind.Unknown;
        }

        public override WorkItemKind Kind
        {
            get { return _kind; }
        }

        public override ulong Callback
        {
            get { return _callback; }
        }

        public override ulong Data
        {
            get { return _data; }
        }
    }
}
