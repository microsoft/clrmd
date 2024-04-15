// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

#pragma warning disable CS0169 // field is never used
#pragma warning disable CS0649 // field is never assigned
namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is required for vtable layout")]
    [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "VTable Layout")]
    internal readonly unsafe struct IDebugControlVtable
    {
        private readonly nint QueryInterface;
        private readonly nint AddRef;
        private readonly nint Release;

        /* IDebugControl */
        private readonly nint GetInterrupt;
        private readonly nint SetInterrupt;
        private readonly nint GetInterruptTimeout;
        private readonly nint SetInterruptTimeout;
        private readonly nint GetLogFile;
        private readonly nint OpenLogFile;
        private readonly nint CloseLogFile;
        private readonly nint GetLogMask;
        private readonly nint SetLogMask;
        private readonly nint Input;
        private readonly nint ReturnInput;
        private readonly nint Output;
        private readonly nint OutputVaList;
        private readonly nint ControlledOutput;
        private readonly nint ControlledOutputVaList;
        private readonly nint OutputPrompt;
        private readonly nint OutputPromptVaList;
        private readonly nint GetPromptText;
        private readonly nint OutputCurrentState;
        private readonly nint OutputVersionInformation;
        private readonly nint GetNotifyEventHandle;
        private readonly nint SetNotifyEventHandle;
        private readonly nint Assemble;
        private readonly nint Disassemble;
        private readonly nint GetDisassembleEffectiveOffset;
        private readonly nint OutputDisassembly;
        private readonly nint OutputDisassemblyLines;
        private readonly nint GetNearInstruction;
        private readonly nint GetStackTrace;
        private readonly nint GetReturnOffset;
        private readonly nint OutputStackTrace;
        private readonly nint GetDebuggeeType;
        private readonly nint GetActualProcessorType;
        private readonly nint GetExecutingProcessorType;
        private readonly nint GetNumberPossibleExecutingProcessorTypes;
        private readonly nint GetPossibleExecutingProcessorTypes;
        public readonly delegate* unmanaged[Stdcall]<nint, out uint, int> GetNumberProcessors;
        public readonly delegate* unmanaged[Stdcall]<nint, uint*, uint*, uint*, char*, int, int*, int*, byte*, int, int*, int> GetSystemVersion;
        private readonly nint GetPageSize;
        public readonly delegate* unmanaged[Stdcall]<nint, int> IsPointer64Bit;
        private readonly nint ReadBugCheckData;
        private readonly nint GetNumberSupportedProcessorTypes;
        private readonly nint GetSupportedProcessorTypes;
        private readonly nint GetProcessorTypeNames;
        public readonly delegate* unmanaged[Stdcall]<nint, ImageFileMachine*, int> GetEffectiveProcessorType;
        private readonly nint SetEffectiveProcessorType;
        public readonly delegate* unmanaged[Stdcall]<nint, out DEBUG_STATUS, int> GetExecutionStatus;
        private readonly nint SetExecutionStatus;
        private readonly nint GetCodeLevel;
        private readonly nint SetCodeLevel;
        private readonly nint GetEngineOptions;
        public readonly delegate* unmanaged[Stdcall]<nint, DEBUG_ENGOPT, int> AddEngineOptions;
        private readonly nint RemoveEngineOptions;
        private readonly nint SetEngineOptions;
        private readonly nint GetSystemErrorControl;
        private readonly nint SetSystemErrorControl;
        private readonly nint GetTextMacro;
        private readonly nint SetTextMacro;
        private readonly nint GetRadix;
        private readonly nint SetRadix;
        private readonly nint Evaluate;
        private readonly nint CoerceValue;
        private readonly nint CoerceValues;
        private readonly nint Execute;
        private readonly nint ExecuteCommandFile;
        private readonly nint GetNumberBreakpoints;
        private readonly nint GetBreakpointByIndex;
        private readonly nint GetBreakpointById;
        private readonly nint GetBreakpointParameters;
        private readonly nint AddBreakpoint;
        private readonly nint RemoveBreakpoint;
        private readonly nint AddExtension;
        private readonly nint RemoveExtension;
        private readonly nint GetExtensionByPath;
        private readonly nint CallExtension;
        private readonly nint GetExtensionFunction;
        private readonly nint GetWindbgExtensionApis32;
        private readonly nint GetWindbgExtensionApis64;
        private readonly nint GetNumberEventFilters;
        private readonly nint GetEventFilterText;
        private readonly nint GetEventFilterCommand;
        private readonly nint SetEventFilterCommand;
        private readonly nint GetSpecificFilterParameters;
        private readonly nint SetSpecificFilterParameters;
        private readonly nint GetSpecificEventFilterArgument;
        private readonly nint SetSpecificEventFilterArgument;
        private readonly nint GetExceptionFilterParameters;
        private readonly nint SetExceptionFilterParameters;
        private readonly nint GetExceptionFilterSecondCommand;
        private readonly nint SetExceptionFilterSecondCommand;
        public readonly delegate* unmanaged[Stdcall]<nint, uint, uint, int> WaitForEvent;
        private readonly nint GetLastEventInformation;

        /* IDebugControl2 */
        public readonly delegate* unmanaged[Stdcall]<nint, out uint, int> GetCurrentTimeDate;
        public readonly delegate* unmanaged[Stdcall]<nint, out uint, int> GetCurrentSystemUpTime;
        private readonly nint GetDumpFormatFlags;
        private readonly nint GetNumberTextReplacements;
        private readonly nint GetTextReplacement;
        private readonly nint SetTextReplacement;
        private readonly nint RemoveTextReplacements;
        private readonly nint OutputTextReplacements;

        /* IDebugControl3 */
        private readonly nint GetAssemblyOptions;
        private readonly nint AddAssemblyOptions;
        private readonly nint RemoveAssemblyOptions;
        private readonly nint SetAssemblyOptions;
        private readonly nint GetExpressionSyntax;
        private readonly nint SetExpressionSyntax;
        private readonly nint SetExpressionSyntaxByName;
        private readonly nint GetNumberExpressionSyntaxes;
        private readonly nint GetExpressionSyntaxNames;
        private readonly nint GetNumberEvents;
        private readonly nint GetEventIndexDescription;
        private readonly nint GetCurrentEventIndex;
        private readonly nint SetNextEventIndex;

        /* IDebugControl4 */
        private readonly nint GetLogFileWide;
        private readonly nint OpenLogFileWide;
        private readonly nint InputWide;
        private readonly nint ReturnInputWide;
        public readonly delegate* unmanaged[Stdcall]<nint, DEBUG_OUTPUT, char*, int> OutputWide;
        private readonly nint OutputVaListWide;
        public readonly delegate* unmanaged[Stdcall]<nint, DEBUG_OUTCTL, DEBUG_OUTPUT, char*, int> ControlledOutputWide;
        private readonly nint ControlledOutputVaListWide;
        private readonly nint OutputPromptWide;
        private readonly nint OutputPromptVaListWide;
        private readonly nint GetPromptTextWide;
        private readonly nint AssembleWide;
        private readonly nint DisassembleWide;
        private readonly nint GetProcessorTypeNamesWide;
        private readonly nint GetTextMacroWide;
        private readonly nint SetTextMacroWide;
        private readonly nint EvaluateWide;
        public readonly delegate* unmanaged[Stdcall]<nint, DEBUG_OUTCTL, char*, DEBUG_EXECUTE, int> ExecuteWide;
        private readonly nint ExecuteCommandFileWide;
        private readonly nint GetBreakpointByIndex2;
        private readonly nint GetBreakpointById2;
        private readonly nint AddBreakpoint2;
        private readonly nint RemoveBreakpoint2;
        private readonly nint AddExtensionWide;
        private readonly nint GetExtensionByPathWide;
        private readonly nint CallExtensionWide;
        private readonly nint GetExtensionFunctionWide;
        private readonly nint GetEventFilterTextWide;
        private readonly nint GetEventFilterCommandWide;
        private readonly nint SetEventFilterCommandWide;
        private readonly nint GetSpecificEventFilterArgumentWide;
        private readonly nint SetSpecificEventFilterArgumentWide;
        private readonly nint GetExceptionFilterSecondCommandWide;
        private readonly nint SetExceptionFilterSecondCommandWide;
        public readonly delegate* unmanaged[Stdcall]<nint, out DEBUG_EVENT, out uint, out uint, byte *, int, out int, char*, int, out int, int> GetLastEventInformationWide;
        private readonly nint GetTextReplacementWide;
        private readonly nint SetTextReplacementWide;
        private readonly nint SetExpressionSyntaxByNameWide;
        private readonly nint GetExpressionSyntaxNamesWide;
        private readonly nint GetEventIndexDescriptionWide;
        private readonly nint GetLogFile2;
        private readonly nint OpenLogFile2;
        private readonly nint GetLogFile2Wide;
        private readonly nint OpenLogFile2Wide;
        private readonly nint GetSystemVersionValues;
        private readonly nint GetSystemVersionString;
        private readonly nint GetSystemVersionStringWide;
        private readonly nint GetContextStackTrace;
        private readonly nint OutputContextStackTrace;
        private readonly nint GetStoredEventInformation;
        private readonly nint GetManagedStatus;
        private readonly nint GetManagedStatusWide;
        private readonly nint ResetManagedStatus;

        /* IDebugControl5 */
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, ulong, ulong, DEBUG_STACK_FRAME_EX*, int, out int, int> GetStackTraceEx;
        private readonly nint OutputStackTraceEx;
        private readonly nint GetContextStackTraceEx;
        private readonly nint OutputContextStackTraceEx;
        private readonly nint GetBreakpointByGuid;
    }
}