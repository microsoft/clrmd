// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopNativeWorkItem : NativeWorkItem
    {
        public DesktopNativeWorkItem(DacpWorkRequestData result)
        {
            Callback = result.Function;
            Data = result.Context;

            switch (result.FunctionType)
            {
                default:
                case WorkRequestFunctionTypes.UNKNOWNWORKITEM:
                    Kind = WorkItemKind.Unknown;
                    break;

                case WorkRequestFunctionTypes.TIMERDELETEWORKITEM:
                    Kind = WorkItemKind.TimerDelete;
                    break;

                case WorkRequestFunctionTypes.QUEUEUSERWORKITEM:
                    Kind = WorkItemKind.QueueUserWorkItem;
                    break;

                case WorkRequestFunctionTypes.ASYNCTIMERCALLBACKCOMPLETION:
                    Kind = WorkItemKind.AsyncTimer;
                    break;

                case WorkRequestFunctionTypes.ASYNCCALLBACKCOMPLETION:
                    Kind = WorkItemKind.AsyncCallback;
                    break;
            }
        }

        public DesktopNativeWorkItem(WorkRequestData result)
        {
            Callback = result.Function;
            Data = result.Context;
            Kind = WorkItemKind.Unknown;
        }

        public override WorkItemKind Kind { get; }

        public override ulong Callback { get; }

        public override ulong Data { get; }
    }
}