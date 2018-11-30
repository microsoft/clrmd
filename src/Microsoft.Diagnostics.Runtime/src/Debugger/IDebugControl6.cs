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
    [Guid("bc0d583f-126d-43a1-9cc4-a860ab1d537b")]
    public interface IDebugControl6 : IDebugControl5
    {
        /* IDebugControl */

        [PreserveSig]
        new int GetInterrupt();

        [PreserveSig]
        new int SetInterrupt(
            [In] DEBUG_INTERRUPT Flags);

        [PreserveSig]
        new int GetInterruptTimeout(
            [Out] out uint Seconds);

        [PreserveSig]
        new int SetInterruptTimeout(
            [In] uint Seconds);

        [PreserveSig]
        new int GetLogFile(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint FileSize,
            [Out][MarshalAs(UnmanagedType.Bool)] out bool Append);

        [PreserveSig]
        new int OpenLogFile(
            [In][MarshalAs(UnmanagedType.LPStr)] string File,
            [In][MarshalAs(UnmanagedType.Bool)] bool Append);

        [PreserveSig]
        new int CloseLogFile();

        [PreserveSig]
        new int GetLogMask(
            [Out] out DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int SetLogMask(
            [In] DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int Input(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint InputSize);

        [PreserveSig]
        new int ReturnInput(
            [In][MarshalAs(UnmanagedType.LPStr)] string Buffer);

        [PreserveSig]
        new int Output(
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        new int OutputVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int ControlledOutput(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        new int ControlledOutputVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int OutputPrompt(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        new int OutputPromptVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int GetPromptText(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint TextSize);

        [PreserveSig]
        new int OutputCurrentState(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_CURRENT Flags);

        [PreserveSig]
        new int OutputVersionInformation(
            [In] DEBUG_OUTCTL OutputControl);

        [PreserveSig]
        new int GetNotifyEventHandle(
            [Out] out ulong Handle);

        [PreserveSig]
        new int SetNotifyEventHandle(
            [In] ulong Handle);

        [PreserveSig]
        new int Assemble(
            [In] ulong Offset,
            [In][MarshalAs(UnmanagedType.LPStr)] string Instr,
            [Out] out ulong EndOffset);

        [PreserveSig]
        new int Disassemble(
            [In] ulong Offset,
            [In] DEBUG_DISASM Flags,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint DisassemblySize,
            [Out] out ulong EndOffset);

        [PreserveSig]
        new int GetDisassembleEffectiveOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        new int OutputDisassembly(
            [In] DEBUG_OUTCTL OutputControl,
            [In] ulong Offset,
            [In] DEBUG_DISASM Flags,
            [Out] out ulong EndOffset);

        [PreserveSig]
        new int OutputDisassemblyLines(
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
        new int GetNearInstruction(
            [In] ulong Offset,
            [In] int Delta,
            [Out] out ulong NearOffset);

        [PreserveSig]
        new int GetStackTrace(
            [In] ulong FrameOffset,
            [In] ulong StackOffset,
            [In] ulong InstructionOffset,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_STACK_FRAME[] Frames,
            [In] int FrameSize,
            [Out] out uint FramesFilled);

        [PreserveSig]
        new int GetReturnOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        new int OutputStackTrace(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME[] Frames,
            [In] int FramesSize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        new int GetDebuggeeType(
            [Out] out DEBUG_CLASS Class,
            [Out] out DEBUG_CLASS_QUALIFIER Qualifier);

        [PreserveSig]
        new int GetActualProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        new int GetExecutingProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        new int GetNumberPossibleExecutingProcessorTypes(
            [Out] out uint Number);

        [PreserveSig]
        new int GetPossibleExecutingProcessorTypes(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            IMAGE_FILE_MACHINE[] Types);

        [PreserveSig]
        new int GetNumberProcessors(
            [Out] out uint Number);

        [PreserveSig]
        new int GetSystemVersion(
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
        new int GetPageSize(
            [Out] out uint Size);

        [PreserveSig]
        new int IsPointer64Bit();

        [PreserveSig]
        new int ReadBugCheckData(
            [Out] out uint Code,
            [Out] out ulong Arg1,
            [Out] out ulong Arg2,
            [Out] out ulong Arg3,
            [Out] out ulong Arg4);

        [PreserveSig]
        new int GetNumberSupportedProcessorTypes(
            [Out] out uint Number);

        [PreserveSig]
        new int GetSupportedProcessorTypes(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            IMAGE_FILE_MACHINE[] Types);

        [PreserveSig]
        new int GetProcessorTypeNames(
            [In] IMAGE_FILE_MACHINE Type,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder FullNameBuffer,
            [In] int FullNameBufferSize,
            [Out] out uint FullNameSize,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder AbbrevNameBuffer,
            [In] int AbbrevNameBufferSize,
            [Out] out uint AbbrevNameSize);

        [PreserveSig]
        new int GetEffectiveProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        new int SetEffectiveProcessorType(
            [In] IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        new int GetExecutionStatus(
            [Out] out DEBUG_STATUS Status);

        [PreserveSig]
        new int SetExecutionStatus(
            [In] DEBUG_STATUS Status);

        [PreserveSig]
        new int GetCodeLevel(
            [Out] out DEBUG_LEVEL Level);

        [PreserveSig]
        new int SetCodeLevel(
            [In] DEBUG_LEVEL Level);

        [PreserveSig]
        new int GetEngineOptions(
            [Out] out DEBUG_ENGOPT Options);

        [PreserveSig]
        new int AddEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        new int RemoveEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        new int SetEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        new int GetSystemErrorControl(
            [Out] out ERROR_LEVEL OutputLevel,
            [Out] out ERROR_LEVEL BreakLevel);

        [PreserveSig]
        new int SetSystemErrorControl(
            [In] ERROR_LEVEL OutputLevel,
            [In] ERROR_LEVEL BreakLevel);

        [PreserveSig]
        new int GetTextMacro(
            [In] uint Slot,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint MacroSize);

        [PreserveSig]
        new int SetTextMacro(
            [In] uint Slot,
            [In][MarshalAs(UnmanagedType.LPStr)] string Macro);

        [PreserveSig]
        new int GetRadix(
            [Out] out uint Radix);

        [PreserveSig]
        new int SetRadix(
            [In] uint Radix);

        [PreserveSig]
        new int Evaluate(
            [In][MarshalAs(UnmanagedType.LPStr)] string Expression,
            [In] DEBUG_VALUE_TYPE DesiredType,
            [Out] out DEBUG_VALUE Value,
            [Out] out uint RemainderIndex);

        [PreserveSig]
        new int CoerceValue(
            [In] DEBUG_VALUE In,
            [In] DEBUG_VALUE_TYPE OutType,
            [Out] out DEBUG_VALUE Out);

        [PreserveSig]
        new int CoerceValues(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] In,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE_TYPE[] OutType,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_VALUE[] Out);

        [PreserveSig]
        new int Execute(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPStr)] string Command,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        new int ExecuteCommandFile(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPStr)] string CommandFile,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        new int GetNumberBreakpoints(
            [Out] out uint Number);

        [PreserveSig]
        new int GetBreakpointByIndex(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugBreakpoint bp);

        [PreserveSig]
        new int GetBreakpointById(
            [In] uint Id,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugBreakpoint bp);

        [PreserveSig]
        new int GetBreakpointParameters(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Ids,
            [In] uint Start,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_BREAKPOINT_PARAMETERS[] Params);

        [PreserveSig]
        new int AddBreakpoint(
            [In] DEBUG_BREAKPOINT_TYPE Type,
            [In] uint DesiredId,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugBreakpoint Bp);

        [PreserveSig]
        new int RemoveBreakpoint(
            [In][MarshalAs(UnmanagedType.Interface)]
            IDebugBreakpoint Bp);

        [PreserveSig]
        new int AddExtension(
            [In][MarshalAs(UnmanagedType.LPStr)] string Path,
            [In] uint Flags,
            [Out] out ulong Handle);

        [PreserveSig]
        new int RemoveExtension(
            [In] ulong Handle);

        [PreserveSig]
        new int GetExtensionByPath(
            [In][MarshalAs(UnmanagedType.LPStr)] string Path,
            [Out] out ulong Handle);

        [PreserveSig]
        new int CallExtension(
            [In] ulong Handle,
            [In][MarshalAs(UnmanagedType.LPStr)] string Function,
            [In][MarshalAs(UnmanagedType.LPStr)] string Arguments);

        [PreserveSig]
        new int GetExtensionFunction(
            [In] ulong Handle,
            [In][MarshalAs(UnmanagedType.LPStr)] string FuncName,
            [Out] out IntPtr Function);

        [PreserveSig]
        new int GetWindbgExtensionApis32(
            [In][Out] ref WINDBG_EXTENSION_APIS Api);

        /* Must be In and Out as the nSize member has to be initialized */

        [PreserveSig]
        new int GetWindbgExtensionApis64(
            [In][Out] ref WINDBG_EXTENSION_APIS Api);

        /* Must be In and Out as the nSize member has to be initialized */

        [PreserveSig]
        new int GetNumberEventFilters(
            [Out] out uint SpecificEvents,
            [Out] out uint SpecificExceptions,
            [Out] out uint ArbitraryExceptions);

        [PreserveSig]
        new int GetEventFilterText(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint TextSize);

        [PreserveSig]
        new int GetEventFilterCommand(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint CommandSize);

        [PreserveSig]
        new int SetEventFilterCommand(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        new int GetSpecificFilterParameters(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_SPECIFIC_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        new int SetSpecificFilterParameters(
            [In] uint Start,
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_SPECIFIC_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        new int GetSpecificEventFilterArgument(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint ArgumentSize);

        [PreserveSig]
        new int SetSpecificEventFilterArgument(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPStr)] string Argument);

        [PreserveSig]
        new int GetExceptionFilterParameters(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Codes,
            [In] uint Start,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_EXCEPTION_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        new int SetExceptionFilterParameters(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_EXCEPTION_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        new int GetExceptionFilterSecondCommand(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint CommandSize);

        [PreserveSig]
        new int SetExceptionFilterSecondCommand(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        new int WaitForEvent(
            [In] DEBUG_WAIT Flags,
            [In] uint Timeout);

        [PreserveSig]
        new int GetLastEventInformation(
            [Out] out DEBUG_EVENT Type,
            [Out] out uint ProcessId,
            [Out] out uint ThreadId,
            [In] IntPtr ExtraInformation,
            [In] uint ExtraInformationSize,
            [Out] out uint ExtraInformationUsed,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Description,
            [In] int DescriptionSize,
            [Out] out uint DescriptionUsed);

        /* IDebugControl2 */

        [PreserveSig]
        new int GetCurrentTimeDate(
            [Out] out uint TimeDate);

        [PreserveSig]
        new int GetCurrentSystemUpTime(
            [Out] out uint UpTime);

        [PreserveSig]
        new int GetDumpFormatFlags(
            [Out] out DEBUG_FORMAT FormatFlags);

        [PreserveSig]
        new int GetNumberTextReplacements(
            [Out] out uint NumRepl);

        [PreserveSig]
        new int GetTextReplacement(
            [In][MarshalAs(UnmanagedType.LPStr)] string SrcText,
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder SrcBuffer,
            [In] int SrcBufferSize,
            [Out] out uint SrcSize,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder DstBuffer,
            [In] int DstBufferSize,
            [Out] out uint DstSize);

        [PreserveSig]
        new int SetTextReplacement(
            [In][MarshalAs(UnmanagedType.LPStr)] string SrcText,
            [In][MarshalAs(UnmanagedType.LPStr)] string DstText);

        [PreserveSig]
        new int RemoveTextReplacements();

        [PreserveSig]
        new int OutputTextReplacements(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUT_TEXT_REPL Flags);

        /* IDebugControl3 */

        [PreserveSig]
        new int GetAssemblyOptions(
            [Out] out DEBUG_ASMOPT Options);

        [PreserveSig]
        new int AddAssemblyOptions(
            [In] DEBUG_ASMOPT Options);

        [PreserveSig]
        new int RemoveAssemblyOptions(
            [In] DEBUG_ASMOPT Options);

        [PreserveSig]
        new int SetAssemblyOptions(
            [In] DEBUG_ASMOPT Options);

        [PreserveSig]
        new int GetExpressionSyntax(
            [Out] out DEBUG_EXPR Flags);

        [PreserveSig]
        new int SetExpressionSyntax(
            [In] DEBUG_EXPR Flags);

        [PreserveSig]
        new int SetExpressionSyntaxByName(
            [In][MarshalAs(UnmanagedType.LPStr)] string AbbrevName);

        [PreserveSig]
        new int GetNumberExpressionSyntaxes(
            [Out] out uint Number);

        [PreserveSig]
        new int GetExpressionSyntaxNames(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder FullNameBuffer,
            [In] int FullNameBufferSize,
            [Out] out uint FullNameSize,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder AbbrevNameBuffer,
            [In] int AbbrevNameBufferSize,
            [Out] out uint AbbrevNameSize);

        [PreserveSig]
        new int GetNumberEvents(
            [Out] out uint Events);

        [PreserveSig]
        new int GetEventIndexDescription(
            [In] uint Index,
            [In] DEBUG_EINDEX Which,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint DescSize);

        [PreserveSig]
        new int GetCurrentEventIndex(
            [Out] out uint Index);

        [PreserveSig]
        new int SetNextEventIndex(
            [In] DEBUG_EINDEX Relation,
            [In] uint Value,
            [Out] out uint NextIndex);

        /* IDebugControl4 */

        [PreserveSig]
        new int GetLogFileWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint FileSize,
            [Out][MarshalAs(UnmanagedType.Bool)] out bool Append);

        [PreserveSig]
        new int OpenLogFileWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string File,
            [In][MarshalAs(UnmanagedType.Bool)] bool Append);

        [PreserveSig]
        new int InputWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint InputSize);

        [PreserveSig]
        new int ReturnInputWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Buffer);

        [PreserveSig]
        new int OutputWide(
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Format);

        [PreserveSig]
        new int OutputVaListWide( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int ControlledOutputWide(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Format);

        [PreserveSig]
        new int ControlledOutputVaListWide( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int OutputPromptWide(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Format);

        [PreserveSig]
        new int OutputPromptVaListWide( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int GetPromptTextWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint TextSize);

        [PreserveSig]
        new int AssembleWide(
            [In] ulong Offset,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Instr,
            [Out] out ulong EndOffset);

        [PreserveSig]
        new int DisassembleWide(
            [In] ulong Offset,
            [In] DEBUG_DISASM Flags,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint DisassemblySize,
            [Out] out ulong EndOffset);

        [PreserveSig]
        new int GetProcessorTypeNamesWide(
            [In] IMAGE_FILE_MACHINE Type,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder FullNameBuffer,
            [In] int FullNameBufferSize,
            [Out] out uint FullNameSize,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder AbbrevNameBuffer,
            [In] int AbbrevNameBufferSize,
            [Out] out uint AbbrevNameSize);

        [PreserveSig]
        new int GetTextMacroWide(
            [In] uint Slot,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint MacroSize);

        [PreserveSig]
        new int SetTextMacroWide(
            [In] uint Slot,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Macro);

        [PreserveSig]
        new int EvaluateWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Expression,
            [In] DEBUG_VALUE_TYPE DesiredType,
            [Out] out DEBUG_VALUE Value,
            [Out] out uint RemainderIndex);

        [PreserveSig]
        new int ExecuteWide(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Command,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        new int ExecuteCommandFileWide(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPWStr)] string CommandFile,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        new int GetBreakpointByIndex2(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugBreakpoint2 bp);

        [PreserveSig]
        new int GetBreakpointById2(
            [In] uint Id,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugBreakpoint2 bp);

        [PreserveSig]
        new int AddBreakpoint2(
            [In] DEBUG_BREAKPOINT_TYPE Type,
            [In] uint DesiredId,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugBreakpoint2 Bp);

        [PreserveSig]
        new int RemoveBreakpoint2(
            [In][MarshalAs(UnmanagedType.Interface)]
            IDebugBreakpoint2 Bp);

        [PreserveSig]
        new int AddExtensionWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Path,
            [In] uint Flags,
            [Out] out ulong Handle);

        [PreserveSig]
        new int GetExtensionByPathWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Path,
            [Out] out ulong Handle);

        [PreserveSig]
        new int CallExtensionWide(
            [In] ulong Handle,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Function,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Arguments);

        [PreserveSig]
        new int GetExtensionFunctionWide(
            [In] ulong Handle,
            [In][MarshalAs(UnmanagedType.LPWStr)] string FuncName,
            [Out] out IntPtr Function);

        [PreserveSig]
        new int GetEventFilterTextWide(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint TextSize);

        [PreserveSig]
        new int GetEventFilterCommandWide(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint CommandSize);

        [PreserveSig]
        new int SetEventFilterCommandWide(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Command);

        [PreserveSig]
        new int GetSpecificEventFilterArgumentWide(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint ArgumentSize);

        [PreserveSig]
        new int SetSpecificEventFilterArgumentWide(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Argument);

        [PreserveSig]
        new int GetExceptionFilterSecondCommandWide(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint CommandSize);

        [PreserveSig]
        new int SetExceptionFilterSecondCommandWide(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Command);

        [PreserveSig]
        new int GetLastEventInformationWide(
            [Out] out DEBUG_EVENT Type,
            [Out] out uint ProcessId,
            [Out] out uint ThreadId,
            [In] IntPtr ExtraInformation,
            [In] int ExtraInformationSize,
            [Out] out uint ExtraInformationUsed,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Description,
            [In] int DescriptionSize,
            [Out] out uint DescriptionUsed);

        [PreserveSig]
        new int GetTextReplacementWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string SrcText,
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder SrcBuffer,
            [In] int SrcBufferSize,
            [Out] out uint SrcSize,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder DstBuffer,
            [In] int DstBufferSize,
            [Out] out uint DstSize);

        [PreserveSig]
        new int SetTextReplacementWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string SrcText,
            [In][MarshalAs(UnmanagedType.LPWStr)] string DstText);

        [PreserveSig]
        new int SetExpressionSyntaxByNameWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string AbbrevName);

        [PreserveSig]
        new int GetExpressionSyntaxNamesWide(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder FullNameBuffer,
            [In] int FullNameBufferSize,
            [Out] out uint FullNameSize,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder AbbrevNameBuffer,
            [In] int AbbrevNameBufferSize,
            [Out] out uint AbbrevNameSize);

        [PreserveSig]
        new int GetEventIndexDescriptionWide(
            [In] uint Index,
            [In] DEBUG_EINDEX Which,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint DescSize);

        [PreserveSig]
        new int GetLogFile2(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint FileSize,
            [Out] out DEBUG_LOG Flags);

        [PreserveSig]
        new int OpenLogFile2(
            [In][MarshalAs(UnmanagedType.LPStr)] string File,
            [Out] out DEBUG_LOG Flags);

        [PreserveSig]
        new int GetLogFile2Wide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint FileSize,
            [Out] out DEBUG_LOG Flags);

        [PreserveSig]
        new int OpenLogFile2Wide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string File,
            [Out] out DEBUG_LOG Flags);

        [PreserveSig]
        new int GetSystemVersionValues(
            [Out] out uint PlatformId,
            [Out] out uint Win32Major,
            [Out] out uint Win32Minor,
            [Out] out uint KdMajor,
            [Out] out uint KdMinor);

        [PreserveSig]
        new int GetSystemVersionString(
            [In] DEBUG_SYSVERSTR Which,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint StringSize);

        [PreserveSig]
        new int GetSystemVersionStringWide(
            [In] DEBUG_SYSVERSTR Which,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint StringSize);

        [PreserveSig]
        new int GetContextStackTrace(
            [In] IntPtr StartContext,
            [In] uint StartContextSize,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_STACK_FRAME[] Frames,
            [In] int FrameSize,
            [In] IntPtr FrameContexts,
            [In] uint FrameContextsSize,
            [In] uint FrameContextsEntrySize,
            [Out] out uint FramesFilled);

        [PreserveSig]
        new int OutputContextStackTrace(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME[] Frames,
            [In] int FramesSize,
            [In] IntPtr FrameContexts,
            [In] uint FrameContextsSize,
            [In] uint FrameContextsEntrySize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        new int GetStoredEventInformation(
            [Out] out DEBUG_EVENT Type,
            [Out] out uint ProcessId,
            [Out] out uint ThreadId,
            [In] IntPtr Context,
            [In] uint ContextSize,
            [Out] out uint ContextUsed,
            [In] IntPtr ExtraInformation,
            [In] uint ExtraInformationSize,
            [Out] out uint ExtraInformationUsed);

        [PreserveSig]
        new int GetManagedStatus(
            [Out] out DEBUG_MANAGED Flags,
            [In] DEBUG_MANSTR WhichString,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder String,
            [In] int StringSize,
            [Out] out uint StringNeeded);

        [PreserveSig]
        new int GetManagedStatusWide(
            [Out] out DEBUG_MANAGED Flags,
            [In] DEBUG_MANSTR WhichString,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder String,
            [In] int StringSize,
            [Out] out uint StringNeeded);

        [PreserveSig]
        new int ResetManagedStatus(
            [In] DEBUG_MANRESET Flags);

        /* IDebugControl5 */

        [PreserveSig]
        new int GetStackTraceEx(
            [In] ulong FrameOffset,
            [In] ulong StackOffset,
            [In] ulong InstructionOffset,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_STACK_FRAME_EX[] Frames,
            [In] int FramesSize,
            [Out] out uint FramesFilled);

        [PreserveSig]
        new int OutputStackTraceEx(
            [In] uint OutputControl,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME_EX[] Frames,
            [In] int FramesSize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        new int GetContextStackTraceEx(
            [In] IntPtr StartContext,
            [In] uint StartContextSize,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_STACK_FRAME_EX[] Frames,
            [In] int FramesSize,
            [In] IntPtr FrameContexts,
            [In] uint FrameContextsSize,
            [In] uint FrameContextsEntrySize,
            [Out] out uint FramesFilled);

        [PreserveSig]
        new int OutputContextStackTraceEx(
            [In] uint OutputControl,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME_EX[] Frames,
            [In] int FramesSize,
            [In] IntPtr FrameContexts,
            [In] uint FrameContextsSize,
            [In] uint FrameContextsEntrySize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        new int GetBreakpointByGuid(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            Guid Guid,
            [Out] out IDebugBreakpoint3 Bp);

        /* IDebugControl6 */

        [PreserveSig]
        int GetExecutionStatusEx([Out] out DEBUG_STATUS Status);

        [PreserveSig]
        int GetSynchronizationStatus(
            [Out] out uint SendsAttempted,
            [Out] out uint SecondsSinceLastResponse);
    }
}