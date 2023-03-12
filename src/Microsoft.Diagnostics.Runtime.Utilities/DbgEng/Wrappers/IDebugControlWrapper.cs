// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;
using System.Threading.Tasks;

#pragma warning disable CS0169 // field is never used
#pragma warning disable CS0649 // field is never assigned
namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [DynamicInterfaceCastableImplementation]
    internal unsafe interface IDebugControlWrapper : IDebugControl
    {
        OSPlatform IDebugControl.OSPlatform
        {
            get
            {
                GetVTable(this, out nint self, out IDebugControlVtable* vtable);
                uint platformId = 0, major = 0, minor = 0;
                int spStrCount = 0, buildStrCount = 0;
                int servicePackNumber = 0;

                int hr = vtable->GetSystemVersion(self, &platformId, &major, &minor, null, 0, &spStrCount, &servicePackNumber, null, 0, &buildStrCount);

                return platformId switch
                {
                    0xa => OSPlatform.Linux,
                    _ => OSPlatform.Windows,
                };
            }
        }

        int IDebugControl.PointerSize
        {
            get
            {
                GetVTable(this, out nint self, out IDebugControlVtable* vtable);

                return vtable->IsPointer64Bit(self) == 0 ? 8 : 4;
            }
        }

        ImageFileMachine IDebugControl.CpuType
        {
            get
            {
                GetVTable(this, out nint self, out IDebugControlVtable* vtable);

                ImageFileMachine result = default;
                vtable->GetEffectiveProcessorType(self, &result);
                return result;
            }
        }

        int IDebugControl.AddEngineOptions(DEBUG_ENGOPT options)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);
            return vtable->AddEngineOptions(self, options);
        }

        int IDebugControl.WaitForEvent(TimeSpan timeout)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);

            uint milliseconds = timeout.TotalMilliseconds > uint.MaxValue ? uint.MaxValue : (uint)timeout.TotalMilliseconds;
            return vtable->WaitForEvent(self, 0, milliseconds);
        }

        void IDebugControl.Write(DEBUG_OUTPUT mask, string text)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);

            fixed (char* textPtr = text)
                vtable->OutputWide(self, mask, textPtr);
        }

        void IDebugControl.ControlledOutput(DEBUG_OUTCTL outCtl, DEBUG_OUTPUT dbgOutput, string text)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);

            fixed (char* textPtr = text)
                vtable->ControlledOutputWide(self, outCtl, dbgOutput, textPtr);
        }

        int IDebugControl.GetExecutionStatus(out DEBUG_STATUS status)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);
            return vtable->GetExecutionStatus(self, out status);
        }

        int IDebugControl.Execute(DEBUG_OUTCTL outputControl, string command, DEBUG_EXECUTE flags)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);
            fixed (char* commandPtr = command)
                return vtable->ExecuteWide(self, outputControl, commandPtr, flags);
        }

        private static void GetVTable(object ths, out nint self, out IDebugControlVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugControl;
            vtable = *(IDebugControlVtable**)self;
        }
    }
}
