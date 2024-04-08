// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;

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

        IEnumerable<DEBUG_STACK_FRAME_EX> IDebugControl.EnumerateStackTrace(int maxFrames)
        {
            DEBUG_STACK_FRAME_EX[] frames = ArrayPool<DEBUG_STACK_FRAME_EX>.Shared.Rent(maxFrames);
            int filled = GetFrames(this, frames);

            for (int i = 0; i < Math.Min(filled, maxFrames); i++)
                yield return frames[i];

            ArrayPool<DEBUG_STACK_FRAME_EX>.Shared.Return(frames);
        }

        private static int GetFrames(object thisPtr, DEBUG_STACK_FRAME_EX[] frames)
        {
            GetVTable(thisPtr, out nint self, out IDebugControlVtable* vtable);
            fixed (DEBUG_STACK_FRAME_EX* ptr = frames)
            {
                HResult hr = vtable->GetStackTraceEx(self, 0, 0, 0, ptr, frames.Length, out int filled);
                return hr ? filled : 0;
            }
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

        bool IDebugControl.GetLastEvent(out DEBUG_LAST_EVENT_INFO_EXCEPTION ex, out uint tid, [NotNullWhen(true)] out string? description)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);
            HResult hr = vtable->GetLastEventInformationWide(self, out DEBUG_EVENT evt, out uint pid, out tid, null, 0, out int infoSize, null, 0, out int descLen);
            if (!hr || evt != DEBUG_EVENT.EXCEPTION || infoSize != Unsafe.SizeOf<DEBUG_LAST_EVENT_INFO_EXCEPTION>())
            {
                ex = default;
                description = null;
                return false;
            }


            char[] buffer = ArrayPool<char>.Shared.Rent(descLen);
            try
            {
                ex = default;
                fixed (DEBUG_LAST_EVENT_INFO_EXCEPTION* exPtr = &ex)
                fixed (char* ptr = buffer)
                    hr = vtable->GetLastEventInformationWide(self, out _, out _, out _, (byte*)exPtr, Unsafe.SizeOf<DEBUG_LAST_EVENT_INFO_EXCEPTION>(), out infoSize, ptr, descLen, out _);

                if (hr == 0)
                    description = new(buffer, 0, descLen - 1);
                else
                    description = "";

                return hr == 0;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
        int IDebugControl.GetNumberProcessors(out uint numberProcessors)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);
            return vtable->GetNumberProcessors(self, out numberProcessors);
        }

        int IDebugControl.GetCurrentTimeDate(out DateTime? timeDate)
        {
            timeDate = null;
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);
            HResult hr = vtable->GetCurrentTimeDate(self, out uint timeDateAsUnixTime);
            if (hr == 0)
            {
                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(timeDateAsUnixTime);
                timeDate = dateTimeOffset.DateTime.ToLocalTime();
            }
            return hr;
        }

        int IDebugControl.GetCurrentSystemUpTime(out TimeSpan upTime)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);
            HResult hr = vtable->GetCurrentSystemUpTime(self, out uint upTimeInSeconds);
            upTime = TimeSpan.FromSeconds(upTimeInSeconds);
            return hr;
        }

        private static void GetVTable(object ths, out nint self, out IDebugControlVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugControl;
            vtable = *(IDebugControlVtable**)self;
        }
    }
}