// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use.
    /// </summary>
    public sealed unsafe class ClrDataProcess : CallableCOMWrapper
    {
        private static readonly Guid IID_IXCLRDataProcess = new Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");
        private readonly DacLibrary _library;

        public ClrDataProcess(DacLibrary library, IntPtr pUnknown)
            : base(library?.OwningLibrary, IID_IXCLRDataProcess, pUnknown)
        {
            if (library is null)
                throw new ArgumentNullException(nameof(library));

            _library = library;
        }

        private ref readonly IXCLRDataProcessVtable VTable => ref Unsafe.AsRef<IXCLRDataProcessVtable>(_vtable);

        public ClrDataProcess(DacLibrary library, CallableCOMWrapper toClone) : base(toClone)
        {
            _library = library;
        }

        public SOSDac? GetSOSDacInterface()
        {
            IntPtr result = QueryInterface(SOSDac.IID_ISOSDac);
            if (result == IntPtr.Zero)
                return null;

            try
            {
                return new SOSDac(_library, result);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public SOSDac6? GetSOSDacInterface6()
        {
            IntPtr result = QueryInterface(SOSDac6.IID_ISOSDac6);
            if (result == IntPtr.Zero)
                return null;

            try
            {
                return new SOSDac6(_library, result);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public void Flush()
        {
            InitDelegate(ref _flush, VTable.Flush);
            _flush(Self);
        }

        public HResult Request(uint reqCode, ReadOnlySpan<byte> input, Span<byte> output)
        {
            InitDelegate(ref _request, VTable.Request);

            fixed (byte* pInput = input)
            fixed (byte* pOutput = output)
                return _request(Self, reqCode, input.Length, pInput, output.Length, pOutput);
        }

        public ClrStackWalk? CreateStackWalk(uint id, uint flags)
        {
            InitDelegate(ref _getTask, VTable.GetTaskByOSThreadID);

            if (!_getTask(Self, id, out IntPtr pUnkTask))
                return null;

            using ClrDataTask dataTask = new ClrDataTask(_library, pUnkTask);
            // There's a bug in certain runtimes where we will fail to release data deep in the runtime
            // when a C++ exception occurs while constructing a ClrDataStackWalk.  This is a workaround
            // for the largest of the leaks caused by this issue.
            //     https://github.com/Microsoft/clrmd/issues/47
            int count = AddRef();
            ClrStackWalk? res = dataTask.CreateStackWalk(_library, flags);
            int released = Release();
            if (released == count && res is null)
                Release();
            return res;
        }

        public IEnumerable<ClrDataMethod> EnumerateMethodInstancesByAddress(ClrDataAddress addr)
        {
            InitDelegate(ref _startEnum, VTable.StartEnumMethodInstancesByAddress);
            InitDelegate(ref _enum, VTable.EnumMethodInstanceByAddress);
            InitDelegate(ref _endEnum, VTable.EndEnumMethodInstancesByAddress);

            List<ClrDataMethod> result = new List<ClrDataMethod>(1);

            if (!_startEnum(Self, addr, IntPtr.Zero, out ClrDataAddress handle))
                return result;

            try
            {
                while (_enum(Self, ref handle, out IntPtr method))
                    result.Add(new ClrDataMethod(_library, method));
            }
            finally
            {
                _endEnum(Self, handle);
            }

            return result;
        }

        private FlushDelegate? _flush;
        private GetTaskByOSThreadIDDelegate? _getTask;
        private RequestDelegate? _request;
        private StartEnumMethodInstancesByAddressDelegate? _startEnum;
        private EnumMethodInstanceByAddressDelegate? _enum;
        private EndEnumMethodInstancesByAddressDelegate? _endEnum;

        private delegate HResult StartEnumMethodInstancesByAddressDelegate(IntPtr self, ClrDataAddress address, IntPtr appDomain, out ClrDataAddress handle);
        private delegate HResult EnumMethodInstanceByAddressDelegate(IntPtr self, ref ClrDataAddress handle, out IntPtr method);
        private delegate HResult EndEnumMethodInstancesByAddressDelegate(IntPtr self, ClrDataAddress handle);
        private delegate HResult RequestDelegate(IntPtr self, uint reqCode, int inBufferSize, byte* inBuffer, int outBufferSize, byte* outBuffer);
        private delegate HResult FlushDelegate(IntPtr self);
        private delegate HResult GetTaskByOSThreadIDDelegate(IntPtr self, uint id, out IntPtr pUnknownTask);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IXCLRDataProcessVtable
    {
        public readonly IntPtr Flush;
        private readonly IntPtr Unused_StartEnumTasks;
        private readonly IntPtr EnumTask;
        private readonly IntPtr EndEnumTasks;

        // (uint id, [Out, MarshalAs(UnmanagedType.IUnknown)] out object task);
        public readonly IntPtr GetTaskByOSThreadID;

        private readonly IntPtr GetTaskByUniqueID;
        private readonly IntPtr GetFlags;
        private readonly IntPtr IsSameObject;
        private readonly IntPtr GetManagedObject;
        private readonly IntPtr GetDesiredExecutionState;
        private readonly IntPtr SetDesiredExecutionState;
        private readonly IntPtr GetAddressType;
        private readonly IntPtr GetRuntimeNameByAddress;
        private readonly IntPtr StartEnumAppDomains;
        private readonly IntPtr EnumAppDomain;
        private readonly IntPtr EndEnumAppDomains;
        private readonly IntPtr GetAppDomainByUniqueID;
        private readonly IntPtr StartEnumAssemblie;
        private readonly IntPtr EnumAssembly;
        private readonly IntPtr EndEnumAssemblies;
        private readonly IntPtr StartEnumModules;
        private readonly IntPtr EnumModule;
        private readonly IntPtr EndEnumModules;
        private readonly IntPtr GetModuleByAddress;

        // (ulong address, [In, MarshalAs(UnmanagedType.Interface)] object appDomain, out ulong handle);
        public readonly IntPtr StartEnumMethodInstancesByAddress;

        // (ref ulong handle, [Out, MarshalAs(UnmanagedType.Interface)] out object method);
        public readonly IntPtr EnumMethodInstanceByAddress;

        // (ulong handle);
        public readonly IntPtr EndEnumMethodInstancesByAddress;
        private readonly IntPtr GetDataByAddress;
        private readonly IntPtr GetExceptionStateByExceptionRecord;
        private readonly IntPtr TranslateExceptionRecordToNotification;

        // (uint reqCode, uint inBufferSize, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] inBuffer, uint outBufferSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] outBuffer);
        public readonly IntPtr Request;
    }

    internal interface IXCLRDataProcess_
    {
        void Flush();

        void StartEnumTasks_do_not_use();
        void EnumTask_do_not_use();
        void EndEnumTasks_do_not_use();

        [PreserveSig]
        int GetTaskByOSThreadID(
            uint id,
            [Out][MarshalAs(UnmanagedType.IUnknown)]
            out object task);

        void GetTaskByUniqueID_do_not_use(/*[in] ULONG64 taskID, [out] IXCLRDataTask** task*/);
        void GetFlags_do_not_use(/*[out] ULONG32* flags*/);
        void IsSameObject_do_not_use(/*[in] IXCLRDataProcess* process*/);
        void GetManagedObject_do_not_use(/*[out] IXCLRDataValue** value*/);
        void GetDesiredExecutionState_do_not_use(/*[out] ULONG32* state*/);
        void SetDesiredExecutionState_do_not_use(/*[in] ULONG32 state*/);
        void GetAddressType_do_not_use(/*[in] CLRDATA_ADDRESS address, [out] CLRDataAddressType* type*/);

        void GetRuntimeNameByAddress_do_not_use(

            /*[in] CLRDATA_ADDRESS address, [in] ULONG32 flags, [in] ULONG32 bufLen, [out] ULONG32 *nameLen, [out, size_is(bufLen)] WCHAR nameBuf[], [out] CLRDATA_ADDRESS* displacement*/);

        void StartEnumAppDomains_do_not_use(/*[out] CLRDATA_ENUM* handle*/);
        void EnumAppDomain_do_not_use(/*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataAppDomain** appDomain*/);
        void EndEnumAppDomains_do_not_use(/*[in] CLRDATA_ENUM handle*/);
        void GetAppDomainByUniqueID_do_not_use(/*[in] ULONG64 id, [out] IXCLRDataAppDomain** appDomain*/);
        void StartEnumAssemblie_do_not_uses(/*[out] CLRDATA_ENUM* handle*/);
        void EnumAssembly_do_not_use(/*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataAssembly **assembly*/);
        void EndEnumAssemblies_do_not_use(/*[in] CLRDATA_ENUM handle*/);
        void StartEnumModules_do_not_use(/*[out] CLRDATA_ENUM* handle*/);
        void EnumModule_do_not_use(/*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataModule **mod*/);
        void EndEnumModules_do_not_use(/*[in] CLRDATA_ENUM handle*/);
        void GetModuleByAddress_do_not_use(/*[in] CLRDATA_ADDRESS address, [out] IXCLRDataModule** mod*/);

        [PreserveSig]
        int StartEnumMethodInstancesByAddress(
            ulong address,
            [In][MarshalAs(UnmanagedType.Interface)]
            object appDomain,
            out ulong handle);

        [PreserveSig]
        int EnumMethodInstanceByAddress(
            ref ulong handle,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out object method);

        [PreserveSig]
        int EndEnumMethodInstancesByAddress(ulong handle);

        void GetDataByAddress_do_not_use(

            /*[in] CLRDATA_ADDRESS address, [in] ULONG32 flags, [in] IXCLRDataAppDomain* appDomain, [in] IXCLRDataTask* tlsTask, [in] ULONG32 bufLen, [out] ULONG32 *nameLen, [out, size_is(bufLen)] WCHAR nameBuf[], [out] IXCLRDataValue** value, [out] CLRDATA_ADDRESS* displacement*/);

        void GetExceptionStateByExceptionRecord_do_not_use(/*[in] EXCEPTION_RECORD64* record, [out] IXCLRDataExceptionState **exState*/);
        void TranslateExceptionRecordToNotification_do_not_use();

        [PreserveSig]
        int Request(
            uint reqCode,
            uint inBufferSize,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            byte[] inBuffer,
            uint outBufferSize,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            byte[] outBuffer);
    }
}