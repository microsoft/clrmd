// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal unsafe sealed class DebugControl : CallableCOMWrapper
    {
        internal static Guid IID_IDebugControl2 = new Guid("d4366723-44df-4bed-8c7e-4c05424f4588");
        private IDebugControlVTable* VTable => (IDebugControlVTable*)_vtable;
        public DebugControl(RefCountedFreeLibrary library, IntPtr pUnk, DebugSystemObjects sys)
            : base(library, ref IID_IDebugControl2, pUnk)
        {
            _sys = sys;
            SuppressRelease();
        }

        public const int INITIAL_BREAK = 0x20;
        private readonly DebugSystemObjects _sys;

        public IMAGE_FILE_MACHINE GetEffectiveProcessorType()
        {
            InitDelegate(ref _getEffectiveProcessorType, VTable->GetEffectiveProcessorType);

            using IDisposable holder = _sys.Enter();
            int hr = _getEffectiveProcessorType(Self, out IMAGE_FILE_MACHINE result);
            if (hr == 0)
                return result;

            return IMAGE_FILE_MACHINE.UNKNOWN;
        }

        public bool IsPointer64Bit()
        {
            InitDelegate(ref _isPointer64Bit, VTable->IsPointer64Bit);

            using IDisposable holder = _sys.Enter();
            int hr = _isPointer64Bit(Self);
            return hr == 0;
        }

        public bool WaitForEvent(uint timeout)
        {
            InitDelegate(ref _waitForEvent, VTable->WaitForEvent);

            using IDisposable holder = _sys.Enter();
            return _waitForEvent(Self, 0, timeout) == 0;
        }

        public void AddEngineOptions(int options)
        {
            InitDelegate(ref _addEngineOptions, VTable->AddEngineOptions);

            using IDisposable holder = _sys.Enter();
            int hr = _addEngineOptions(Self, options);
            Debug.Assert(hr == 0);
        }

        public DEBUG_FORMAT GetDumpFormat()
        {
            InitDelegate(ref _getDumpFormat, VTable->GetDumpFormatFlags);

            using IDisposable holder = _sys.Enter();
            int hr = _getDumpFormat(Self, out DEBUG_FORMAT result);
            if (hr == 0)
                return result;

            return DEBUG_FORMAT.DEFAULT;
        }

        public DEBUG_CLASS_QUALIFIER GetDebuggeeClassQualifier()
        {
            InitDelegate(ref _getDebuggeeType, VTable->GetDebuggeeType);

            using IDisposable holder = _sys.Enter();
            int hr = _getDebuggeeType(Self, out _, out DEBUG_CLASS_QUALIFIER result);
            Debug.Assert(hr == 0);
            return result;
        }


        private GetEffectiveProcessorTypeDelegate _getEffectiveProcessorType;
        private IsPointer64BitDelegate _isPointer64Bit;
        private WaitForEventDelegate _waitForEvent;
        private AddEngineOptionsDelegate _addEngineOptions;
        private GetDebuggeeTypeDelegate _getDebuggeeType;
        private GetDumpFormatFlagsDelegate _getDumpFormat;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetEffectiveProcessorTypeDelegate(IntPtr self, out IMAGE_FILE_MACHINE Type);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int IsPointer64BitDelegate(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int WaitForEventDelegate(IntPtr self, int flags, uint timeout);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int AddEngineOptionsDelegate(IntPtr self, int options);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetDebuggeeTypeDelegate(IntPtr self, out uint cls, out DEBUG_CLASS_QUALIFIER qualifier);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetDumpFormatFlagsDelegate(IntPtr self, out DEBUG_FORMAT format);
    }


#pragma warning disable CS0169
#pragma warning disable CS0649
#pragma warning disable IDE0051
#pragma warning disable CA1823

    internal struct IDebugControlVTable
    {
        public readonly IntPtr GetInterrupt;
        public readonly IntPtr SetInterrupt;
        public readonly IntPtr GetInterruptTimeout;
        public readonly IntPtr SetInterruptTimeout;
        public readonly IntPtr GetLogFile;
        public readonly IntPtr OpenLogFile;
        public readonly IntPtr CloseLogFile;
        public readonly IntPtr GetLogMask;
        public readonly IntPtr SetLogMask;
        public readonly IntPtr Input;
        public readonly IntPtr ReturnInput;
        public readonly IntPtr Output;
        public readonly IntPtr OutputVaList;
        public readonly IntPtr ControlledOutput;
        public readonly IntPtr ControlledOutputVaList;
        public readonly IntPtr OutputPrompt;
        public readonly IntPtr OutputPromptVaList;
        public readonly IntPtr GetPromptText;
        public readonly IntPtr OutputCurrentState;
        public readonly IntPtr OutputVersionInformation;
        public readonly IntPtr GetNotifyEventHandle;
        public readonly IntPtr SetNotifyEventHandle;
        public readonly IntPtr Assemble;
        public readonly IntPtr Disassemble;
        public readonly IntPtr GetDisassembleEffectiveOffset;
        public readonly IntPtr OutputDisassembly;
        public readonly IntPtr OutputDisassemblyLines;
        public readonly IntPtr GetNearInstruction;
        public readonly IntPtr GetStackTrace;
        public readonly IntPtr GetReturnOffset;
        public readonly IntPtr OutputStackTrace;
        public readonly IntPtr GetDebuggeeType;
        public readonly IntPtr GetActualProcessorType;
        public readonly IntPtr GetExecutingProcessorType;
        public readonly IntPtr GetNumberPossibleExecutingProcessorTypes;
        public readonly IntPtr GetPossibleExecutingProcessorTypes;
        public readonly IntPtr GetNumberProcessors;
        public readonly IntPtr GetSystemVersion;
        public readonly IntPtr GetPageSize;
        public readonly IntPtr IsPointer64Bit;
        public readonly IntPtr ReadBugCheckData;
        public readonly IntPtr GetNumberSupportedProcessorTypes;
        public readonly IntPtr GetSupportedProcessorTypes;
        public readonly IntPtr GetProcessorTypeNames;
        public readonly IntPtr GetEffectiveProcessorType;
        public readonly IntPtr SetEffectiveProcessorType;
        public readonly IntPtr GetExecutionStatus;
        public readonly IntPtr SetExecutionStatus;
        public readonly IntPtr GetCodeLevel;
        public readonly IntPtr SetCodeLevel;
        public readonly IntPtr GetEngineOptions;
        public readonly IntPtr AddEngineOptions;
        public readonly IntPtr RemoveEngineOptions;
        public readonly IntPtr SetEngineOptions;
        public readonly IntPtr GetSystemErrorControl;
        public readonly IntPtr SetSystemErrorControl;
        public readonly IntPtr GetTextMacro;
        public readonly IntPtr SetTextMacro;
        public readonly IntPtr GetRadix;
        public readonly IntPtr SetRadix;
        public readonly IntPtr Evaluate;
        public readonly IntPtr CoerceValue;
        public readonly IntPtr CoerceValues;
        public readonly IntPtr Execute;
        public readonly IntPtr ExecuteCommandFile;
        public readonly IntPtr GetNumberBreakpoints;
        public readonly IntPtr GetBreakpointByIndex;
        public readonly IntPtr GetBreakpointById;
        public readonly IntPtr GetBreakpointParameters;
        public readonly IntPtr AddBreakpoint;
        public readonly IntPtr RemoveBreakpoint;
        public readonly IntPtr AddExtension;
        public readonly IntPtr RemoveExtension;
        public readonly IntPtr GetExtensionByPath;
        public readonly IntPtr CallExtension;
        public readonly IntPtr GetExtensionFunction;
        public readonly IntPtr GetWindbgExtensionApis32;
        public readonly IntPtr GetWindbgExtensionApis64;
        public readonly IntPtr GetNumberEventFilters;
        public readonly IntPtr GetEventFilterText;
        public readonly IntPtr GetEventFilterCommand;
        public readonly IntPtr SetEventFilterCommand;
        public readonly IntPtr GetSpecificFilterParameters;
        public readonly IntPtr SetSpecificFilterParameters;
        public readonly IntPtr GetSpecificEventFilterArgument;
        public readonly IntPtr SetSpecificEventFilterArgument;
        public readonly IntPtr GetExceptionFilterParameters;
        public readonly IntPtr SetExceptionFilterParameters;
        public readonly IntPtr GetExceptionFilterSecondCommand;
        public readonly IntPtr SetExceptionFilterSecondCommand;
        public readonly IntPtr WaitForEvent;
        public readonly IntPtr GetLastEventInformation;

        public readonly IntPtr GetCurrentTimeDate;
        public readonly IntPtr GetCurrentSystemUpTime;
        public readonly IntPtr GetDumpFormatFlags;
        public readonly IntPtr GetNumberTextReplacements;
        public readonly IntPtr GetTextReplacement;
        public readonly IntPtr SetTextReplacement;
        public readonly IntPtr RemoveTextReplacements;
        public readonly IntPtr OutputTextReplacements;


    }
}
