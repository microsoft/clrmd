// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("5182e668-105e-416e-ad92-24ef800424ba")]
    public interface IDebugControl
    {
        /* IDebugControl */

        [PreserveSig]
        int GetInterrupt();

        [PreserveSig]
        int SetInterrupt(
            [In] DEBUG_INTERRUPT Flags);

        [PreserveSig]
        int GetInterruptTimeout(
            out uint Seconds);

        [PreserveSig]
        int SetInterruptTimeout(
            [In] uint Seconds);

        [PreserveSig]
        int GetLogFile(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            out uint FileSize,
            [Out][MarshalAs(UnmanagedType.Bool)] out bool Append);

        [PreserveSig]
        int OpenLogFile(
            [In][MarshalAs(UnmanagedType.LPStr)] string File,
            [In][MarshalAs(UnmanagedType.Bool)] bool Append);

        [PreserveSig]
        int CloseLogFile();

        [PreserveSig]
        int GetLogMask(
            out DEBUG_OUTPUT Mask);

        [PreserveSig]
        int SetLogMask(
            [In] DEBUG_OUTPUT Mask);

        [PreserveSig]
        int Input(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            out uint InputSize);

        [PreserveSig]
        int ReturnInput(
            [In][MarshalAs(UnmanagedType.LPStr)] string Buffer);

        [PreserveSig]
        int Output(
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        int OutputVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        int ControlledOutput(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        int ControlledOutputVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        int OutputPrompt(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        int OutputPromptVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        int GetPromptText(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            out uint TextSize);

        [PreserveSig]
        int OutputCurrentState(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_CURRENT Flags);

        [PreserveSig]
        int OutputVersionInformation(
            [In] DEBUG_OUTCTL OutputControl);

        [PreserveSig]
        int GetNotifyEventHandle(
            out ulong Handle);

        [PreserveSig]
        int SetNotifyEventHandle(
            [In] ulong Handle);

        [PreserveSig]
        int Assemble(
            [In] ulong Offset,
            [In][MarshalAs(UnmanagedType.LPStr)] string Instr,
            out ulong EndOffset);

        [PreserveSig]
        int Disassemble(
            [In] ulong Offset,
            [In] DEBUG_DISASM Flags,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            out uint DisassemblySize,
            out ulong EndOffset);

        [PreserveSig]
        int GetDisassembleEffectiveOffset(
            out ulong Offset);

        [PreserveSig]
        int OutputDisassembly(
            [In] DEBUG_OUTCTL OutputControl,
            [In] ulong Offset,
            [In] DEBUG_DISASM Flags,
            out ulong EndOffset);

        [PreserveSig]
        int OutputDisassemblyLines(
            [In] DEBUG_OUTCTL OutputControl,
            [In] uint PreviousLines,
            [In] uint TotalLines,
            [In] ulong Offset,
            [In] DEBUG_DISASM Flags,
            out uint OffsetLine,
            out ulong StartOffset,
            out ulong EndOffset,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            ulong[] LineOffsets);

        [PreserveSig]
        int GetNearInstruction(
            [In] ulong Offset,
            [In] int Delta,
            out ulong NearOffset);

        [PreserveSig]
        int GetStackTrace(
            [In] ulong FrameOffset,
            [In] ulong StackOffset,
            [In] ulong InstructionOffset,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_STACK_FRAME[] Frames,
            [In] int FrameSize,
            out uint FramesFilled);

        [PreserveSig]
        int GetReturnOffset(
            out ulong Offset);

        [PreserveSig]
        int OutputStackTrace(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME[] Frames,
            [In] int FramesSize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        int GetDebuggeeType(
            out DEBUG_CLASS Class,
            out DEBUG_CLASS_QUALIFIER Qualifier);

        [PreserveSig]
        int GetActualProcessorType(
            out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int GetExecutingProcessorType(
            out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int GetNumberPossibleExecutingProcessorTypes(
            out uint Number);

        [PreserveSig]
        int GetPossibleExecutingProcessorTypes(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            IMAGE_FILE_MACHINE[] Types);

        [PreserveSig]
        int GetNumberProcessors(
            out uint Number);

        [PreserveSig]
        int GetSystemVersion(
            out uint PlatformId,
            out uint Major,
            out uint Minor,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder ServicePackString,
            [In] int ServicePackStringSize,
            out uint ServicePackStringUsed,
            out uint ServicePackNumber,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder BuildString,
            [In] int BuildStringSize,
            out uint BuildStringUsed);

        [PreserveSig]
        int GetPageSize(
            out uint Size);

        [PreserveSig]
        int IsPointer64Bit();

        [PreserveSig]
        int ReadBugCheckData(
            out uint Code,
            out ulong Arg1,
            out ulong Arg2,
            out ulong Arg3,
            out ulong Arg4);

        [PreserveSig]
        int GetNumberSupportedProcessorTypes(
            out uint Number);

        [PreserveSig]
        int GetSupportedProcessorTypes(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            IMAGE_FILE_MACHINE[] Types);

        [PreserveSig]
        int GetProcessorTypeNames(
            [In] IMAGE_FILE_MACHINE Type,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder FullNameBuffer,
            [In] int FullNameBufferSize,
            out uint FullNameSize,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder AbbrevNameBuffer,
            [In] int AbbrevNameBufferSize,
            out uint AbbrevNameSize);

        [PreserveSig]
        int GetEffectiveProcessorType(
            out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int SetEffectiveProcessorType(
            [In] IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int GetExecutionStatus(
            out DEBUG_STATUS Status);

        [PreserveSig]
        int SetExecutionStatus(
            [In] DEBUG_STATUS Status);

        [PreserveSig]
        int GetCodeLevel(
            out DEBUG_LEVEL Level);

        [PreserveSig]
        int SetCodeLevel(
            [In] DEBUG_LEVEL Level);

        [PreserveSig]
        int GetEngineOptions(
            out DEBUG_ENGOPT Options);

        [PreserveSig]
        int AddEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        int RemoveEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        int SetEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        int GetSystemErrorControl(
            out ERROR_LEVEL OutputLevel,
            out ERROR_LEVEL BreakLevel);

        [PreserveSig]
        int SetSystemErrorControl(
            [In] ERROR_LEVEL OutputLevel,
            [In] ERROR_LEVEL BreakLevel);

        [PreserveSig]
        int GetTextMacro(
            [In] uint Slot,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            out uint MacroSize);

        [PreserveSig]
        int SetTextMacro(
            [In] uint Slot,
            [In][MarshalAs(UnmanagedType.LPStr)] string Macro);

        [PreserveSig]
        int GetRadix(
            out uint Radix);

        [PreserveSig]
        int SetRadix(
            [In] uint Radix);

        [PreserveSig]
        int Evaluate(
            [In][MarshalAs(UnmanagedType.LPStr)] string Expression,
            [In] DEBUG_VALUE_TYPE DesiredType,
            out DEBUG_VALUE Value,
            out uint RemainderIndex);

        [PreserveSig]
        int CoerceValue(
            [In] DEBUG_VALUE In,
            [In] DEBUG_VALUE_TYPE OutType,
            out DEBUG_VALUE Out);

        [PreserveSig]
        int CoerceValues(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] In,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE_TYPE[] OutType,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_VALUE[] Out);

        [PreserveSig]
        int Execute(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPStr)] string Command,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        int ExecuteCommandFile(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPStr)] string CommandFile,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        int GetNumberBreakpoints(
            out uint Number);

        [PreserveSig]
        int GetBreakpointByIndex(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugBreakpoint bp);

        [PreserveSig]
        int GetBreakpointById(
            [In] uint Id,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugBreakpoint bp);

        [PreserveSig]
        int GetBreakpointParameters(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Ids,
            [In] uint Start,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_BREAKPOINT_PARAMETERS[] Params);

        [PreserveSig]
        int AddBreakpoint(
            [In] DEBUG_BREAKPOINT_TYPE Type,
            [In] uint DesiredId,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugBreakpoint Bp);

        [PreserveSig]
        int RemoveBreakpoint(
            [In][MarshalAs(UnmanagedType.Interface)]
            IDebugBreakpoint Bp);

        [PreserveSig]
        int AddExtension(
            [In][MarshalAs(UnmanagedType.LPStr)] string Path,
            [In] uint Flags,
            out ulong Handle);

        [PreserveSig]
        int RemoveExtension(
            [In] ulong Handle);

        [PreserveSig]
        int GetExtensionByPath(
            [In][MarshalAs(UnmanagedType.LPStr)] string Path,
            out ulong Handle);

        [PreserveSig]
        int CallExtension(
            [In] ulong Handle,
            [In][MarshalAs(UnmanagedType.LPStr)] string Function,
            [In][MarshalAs(UnmanagedType.LPStr)] string Arguments);

        [PreserveSig]
        int GetExtensionFunction(
            [In] ulong Handle,
            [In][MarshalAs(UnmanagedType.LPStr)] string FuncName,
            out IntPtr Function);

        [PreserveSig]
        int GetWindbgExtensionApis32(
            [In][Out] ref WINDBG_EXTENSION_APIS Api);

        /* Must be In and Out as the nSize member has to be initialized */

        [PreserveSig]
        int GetWindbgExtensionApis64(
            [In][Out] ref WINDBG_EXTENSION_APIS Api);

        /* Must be In and Out as the nSize member has to be initialized */

        [PreserveSig]
        int GetNumberEventFilters(
            out uint SpecificEvents,
            out uint SpecificExceptions,
            out uint ArbitraryExceptions);

        [PreserveSig]
        int GetEventFilterText(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            out uint TextSize);

        [PreserveSig]
        int GetEventFilterCommand(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            out uint CommandSize);

        [PreserveSig]
        int SetEventFilterCommand(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        int GetSpecificFilterParameters(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_SPECIFIC_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        int SetSpecificFilterParameters(
            [In] uint Start,
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_SPECIFIC_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        int GetSpecificEventFilterArgument(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            out uint ArgumentSize);

        [PreserveSig]
        int SetSpecificEventFilterArgument(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPStr)] string Argument);

        [PreserveSig]
        int GetExceptionFilterParameters(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Codes,
            [In] uint Start,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_EXCEPTION_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        int SetExceptionFilterParameters(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_EXCEPTION_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        int GetExceptionFilterSecondCommand(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            out uint CommandSize);

        [PreserveSig]
        int SetExceptionFilterSecondCommand(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        int WaitForEvent(
            [In] DEBUG_WAIT Flags,
            [In] uint Timeout);

        [PreserveSig]
        int GetLastEventInformation(
            out DEBUG_EVENT Type,
            out uint ProcessId,
            out uint ThreadId,
            [In] IntPtr ExtraInformation,
            [In] uint ExtraInformationSize,
            out uint ExtraInformationUsed,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Description,
            [In] int DescriptionSize,
            out uint DescriptionUsed);
    }
}