// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable 1591

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
            [Out] out uint Seconds);

        [PreserveSig]
        int SetInterruptTimeout(
            [In] uint Seconds);

        [PreserveSig]
        int GetLogFile(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint FileSize,
            [Out][MarshalAs(UnmanagedType.Bool)] out bool Append);

        [PreserveSig]
        int OpenLogFile(
            [In][MarshalAs(UnmanagedType.LPStr)] string File,
            [In][MarshalAs(UnmanagedType.Bool)] bool Append);

        [PreserveSig]
        int CloseLogFile();

        [PreserveSig]
        int GetLogMask(
            [Out] out DEBUG_OUTPUT Mask);

        [PreserveSig]
        int SetLogMask(
            [In] DEBUG_OUTPUT Mask);

        [PreserveSig]
        int Input(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint InputSize);

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
            [Out] out uint TextSize);

        [PreserveSig]
        int OutputCurrentState(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_CURRENT Flags);

        [PreserveSig]
        int OutputVersionInformation(
            [In] DEBUG_OUTCTL OutputControl);

        [PreserveSig]
        int GetNotifyEventHandle(
            [Out] out ulong Handle);

        [PreserveSig]
        int SetNotifyEventHandle(
            [In] ulong Handle);

        [PreserveSig]
        int Assemble(
            [In] ulong Offset,
            [In][MarshalAs(UnmanagedType.LPStr)] string Instr,
            [Out] out ulong EndOffset);

        [PreserveSig]
        int Disassemble(
            [In] ulong Offset,
            [In] DEBUG_DISASM Flags,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint DisassemblySize,
            [Out] out ulong EndOffset);

        [PreserveSig]
        int GetDisassembleEffectiveOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        int OutputDisassembly(
            [In] DEBUG_OUTCTL OutputControl,
            [In] ulong Offset,
            [In] DEBUG_DISASM Flags,
            [Out] out ulong EndOffset);

        [PreserveSig]
        int OutputDisassemblyLines(
            [In] DEBUG_OUTCTL OutputControl,
            [In] uint PreviousLines,
            [In] uint TotalLines,
            [In] ulong Offset,
            [In] DEBUG_DISASM Flags,
            [Out] out uint OffsetLine,
            [Out] out ulong StartOffset,
            [Out] out ulong EndOffset,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            ulong[] LineOffsets);

        [PreserveSig]
        int GetNearInstruction(
            [In] ulong Offset,
            [In] int Delta,
            [Out] out ulong NearOffset);

        [PreserveSig]
        int GetStackTrace(
            [In] ulong FrameOffset,
            [In] ulong StackOffset,
            [In] ulong InstructionOffset,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_STACK_FRAME[] Frames,
            [In] int FrameSize,
            [Out] out uint FramesFilled);

        [PreserveSig]
        int GetReturnOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        int OutputStackTrace(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME[] Frames,
            [In] int FramesSize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        int GetDebuggeeType(
            [Out] out DEBUG_CLASS Class,
            [Out] out DEBUG_CLASS_QUALIFIER Qualifier);

        [PreserveSig]
        int GetActualProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int GetExecutingProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int GetNumberPossibleExecutingProcessorTypes(
            [Out] out uint Number);

        [PreserveSig]
        int GetPossibleExecutingProcessorTypes(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            IMAGE_FILE_MACHINE[] Types);

        [PreserveSig]
        int GetNumberProcessors(
            [Out] out uint Number);

        [PreserveSig]
        int GetSystemVersion(
            [Out] out uint PlatformId,
            [Out] out uint Major,
            [Out] out uint Minor,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder ServicePackString,
            [In] int ServicePackStringSize,
            [Out] out uint ServicePackStringUsed,
            [Out] out uint ServicePackNumber,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder BuildString,
            [In] int BuildStringSize,
            [Out] out uint BuildStringUsed);

        [PreserveSig]
        int GetPageSize(
            [Out] out uint Size);

        [PreserveSig]
        int IsPointer64Bit();

        [PreserveSig]
        int ReadBugCheckData(
            [Out] out uint Code,
            [Out] out ulong Arg1,
            [Out] out ulong Arg2,
            [Out] out ulong Arg3,
            [Out] out ulong Arg4);

        [PreserveSig]
        int GetNumberSupportedProcessorTypes(
            [Out] out uint Number);

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
            [Out] out uint FullNameSize,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder AbbrevNameBuffer,
            [In] int AbbrevNameBufferSize,
            [Out] out uint AbbrevNameSize);

        [PreserveSig]
        int GetEffectiveProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int SetEffectiveProcessorType(
            [In] IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int GetExecutionStatus(
            [Out] out DEBUG_STATUS Status);

        [PreserveSig]
        int SetExecutionStatus(
            [In] DEBUG_STATUS Status);

        [PreserveSig]
        int GetCodeLevel(
            [Out] out DEBUG_LEVEL Level);

        [PreserveSig]
        int SetCodeLevel(
            [In] DEBUG_LEVEL Level);

        [PreserveSig]
        int GetEngineOptions(
            [Out] out DEBUG_ENGOPT Options);

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
            [Out] out ERROR_LEVEL OutputLevel,
            [Out] out ERROR_LEVEL BreakLevel);

        [PreserveSig]
        int SetSystemErrorControl(
            [In] ERROR_LEVEL OutputLevel,
            [In] ERROR_LEVEL BreakLevel);

        [PreserveSig]
        int GetTextMacro(
            [In] uint Slot,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint MacroSize);

        [PreserveSig]
        int SetTextMacro(
            [In] uint Slot,
            [In][MarshalAs(UnmanagedType.LPStr)] string Macro);

        [PreserveSig]
        int GetRadix(
            [Out] out uint Radix);

        [PreserveSig]
        int SetRadix(
            [In] uint Radix);

        [PreserveSig]
        int Evaluate(
            [In][MarshalAs(UnmanagedType.LPStr)] string Expression,
            [In] DEBUG_VALUE_TYPE DesiredType,
            [Out] out DEBUG_VALUE Value,
            [Out] out uint RemainderIndex);

        [PreserveSig]
        int CoerceValue(
            [In] DEBUG_VALUE In,
            [In] DEBUG_VALUE_TYPE OutType,
            [Out] out DEBUG_VALUE Out);

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
            [Out] out uint Number);

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
            [Out] out ulong Handle);

        [PreserveSig]
        int RemoveExtension(
            [In] ulong Handle);

        [PreserveSig]
        int GetExtensionByPath(
            [In][MarshalAs(UnmanagedType.LPStr)] string Path,
            [Out] out ulong Handle);

        [PreserveSig]
        int CallExtension(
            [In] ulong Handle,
            [In][MarshalAs(UnmanagedType.LPStr)] string Function,
            [In][MarshalAs(UnmanagedType.LPStr)] string Arguments);

        [PreserveSig]
        int GetExtensionFunction(
            [In] ulong Handle,
            [In][MarshalAs(UnmanagedType.LPStr)] string FuncName,
            [Out] out IntPtr Function);

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
            [Out] out uint SpecificEvents,
            [Out] out uint SpecificExceptions,
            [Out] out uint ArbitraryExceptions);

        [PreserveSig]
        int GetEventFilterText(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint TextSize);

        [PreserveSig]
        int GetEventFilterCommand(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint CommandSize);

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
            [Out] out uint ArgumentSize);

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
            [Out] out uint CommandSize);

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
            [Out] out DEBUG_EVENT Type,
            [Out] out uint ProcessId,
            [Out] out uint ThreadId,
            [In] IntPtr ExtraInformation,
            [In] uint ExtraInformationSize,
            [Out] out uint ExtraInformationUsed,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Description,
            [In] int DescriptionSize,
            [Out] out uint DescriptionUsed);
    }
}