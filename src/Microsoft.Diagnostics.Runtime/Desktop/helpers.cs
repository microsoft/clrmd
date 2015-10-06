// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class AppDomainHeapWalker
    {
        #region Variables
        private enum InternalHeapTypes
        {
            IndcellHeap,
            LookupHeap,
            ResolveHeap,
            DispatchHeap,
            CacheEntryHeap
        }

        private List<MemoryRegion> _mRegions = new List<MemoryRegion>();
        private DesktopRuntimeBase.LoaderHeapTraverse _mDelegate;
        private ClrMemoryRegionType _mType;
        private ulong _mAppDomain;
        private DesktopRuntimeBase _mRuntime;
        #endregion

        public AppDomainHeapWalker(DesktopRuntimeBase runtime)
        {
            _mRuntime = runtime;
            _mDelegate = new DesktopRuntimeBase.LoaderHeapTraverse(VisitOneHeap);
        }

        public IEnumerable<MemoryRegion> EnumerateHeaps(IAppDomainData appDomain)
        {
            Debug.Assert(appDomain != null);
            _mAppDomain = appDomain.Address;
            _mRegions.Clear();

            // Standard heaps.
            _mType = ClrMemoryRegionType.LowFrequencyLoaderHeap;
            _mRuntime.TraverseHeap(appDomain.LowFrequencyHeap, _mDelegate);

            _mType = ClrMemoryRegionType.HighFrequencyLoaderHeap;
            _mRuntime.TraverseHeap(appDomain.HighFrequencyHeap, _mDelegate);

            _mType = ClrMemoryRegionType.StubHeap;
            _mRuntime.TraverseHeap(appDomain.StubHeap, _mDelegate);

            // Stub heaps.
            _mType = ClrMemoryRegionType.IndcellHeap;
            _mRuntime.TraverseStubHeap(_mAppDomain, (int)InternalHeapTypes.IndcellHeap, _mDelegate);

            _mType = ClrMemoryRegionType.LookupHeap;
            _mRuntime.TraverseStubHeap(_mAppDomain, (int)InternalHeapTypes.LookupHeap, _mDelegate);

            _mType = ClrMemoryRegionType.ResolveHeap;
            _mRuntime.TraverseStubHeap(_mAppDomain, (int)InternalHeapTypes.ResolveHeap, _mDelegate);

            _mType = ClrMemoryRegionType.DispatchHeap;
            _mRuntime.TraverseStubHeap(_mAppDomain, (int)InternalHeapTypes.DispatchHeap, _mDelegate);

            _mType = ClrMemoryRegionType.CacheEntryHeap;
            _mRuntime.TraverseStubHeap(_mAppDomain, (int)InternalHeapTypes.CacheEntryHeap, _mDelegate);

            return _mRegions;
        }

        public IEnumerable<MemoryRegion> EnumerateModuleHeaps(IAppDomainData appDomain, ulong addr)
        {
            Debug.Assert(appDomain != null);
            _mAppDomain = appDomain.Address;
            _mRegions.Clear();

            if (addr == 0)
                return _mRegions;

            IModuleData module = _mRuntime.GetModuleData(addr);
            if (module != null)
            {
                _mType = ClrMemoryRegionType.ModuleThunkHeap;
                _mRuntime.TraverseHeap(module.ThunkHeap, _mDelegate);

                _mType = ClrMemoryRegionType.ModuleLookupTableHeap;
                _mRuntime.TraverseHeap(module.LookupTableHeap, _mDelegate);
            }

            return _mRegions;
        }

        public IEnumerable<MemoryRegion> EnumerateJitHeap(ulong heap)
        {
            _mAppDomain = 0;
            _mRegions.Clear();

            _mType = ClrMemoryRegionType.JitLoaderCodeHeap;
            _mRuntime.TraverseHeap(heap, _mDelegate);

            return _mRegions;
        }

        #region Helper Functions
        private void VisitOneHeap(ulong address, IntPtr size, int isCurrent)
        {
            if (_mAppDomain == 0)
                _mRegions.Add(new MemoryRegion(_mRuntime, address, (ulong)size.ToInt64(), _mType));
            else
                _mRegions.Add(new MemoryRegion(_mRuntime, address, (ulong)size.ToInt64(), _mType, _mAppDomain));
        }
        #endregion

    }

    internal class HandleTableWalker
    {
        #region Variables
        private DesktopRuntimeBase _runtime;
        private ClrHeap _heap;
        private int _max = 10000;
        private VISITHANDLEV2 _mV2Delegate;
        private VISITHANDLEV4 _mV4Delegate;
        #endregion

        #region Properties
        public List<ClrHandle> Handles { get; private set; }
        public byte[] V4Request
        {
            get
            {
                // MULTITHREAD ISSUE
                if (_mV4Delegate == null)
                    _mV4Delegate = new VISITHANDLEV4(VisitHandleV4);

                IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(_mV4Delegate);
                byte[] request = new byte[IntPtr.Size * 2];
                FunctionPointerToByteArray(functionPtr, request, 0);

                return request;
            }
        }


        public byte[] V2Request
        {
            get
            {
                // MULTITHREAD ISSUE
                if (_mV2Delegate == null)
                    _mV2Delegate = new VISITHANDLEV2(VisitHandleV2);

                IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(_mV2Delegate);
                byte[] request = new byte[IntPtr.Size * 2];

                FunctionPointerToByteArray(functionPtr, request, 0);

                return request;
            }
        }
        #endregion

        #region Functions
        public HandleTableWalker(DesktopRuntimeBase dac)
        {
            _runtime = dac;
            _heap = dac.GetHeap();
            Handles = new List<ClrHandle>();
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]

        private delegate int VISITHANDLEV4(ulong HandleAddr, ulong HandleValue, int HandleType, uint ulRefCount, ulong appDomainPtr, IntPtr token);

        private int VisitHandleV4(ulong addr, ulong obj, int hndType, uint refCnt, ulong appDomain, IntPtr unused)
        {
            Debug.Assert(unused == IntPtr.Zero);

            return AddHandle(addr, obj, hndType, refCnt, 0, appDomain);
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]


        private delegate int VISITHANDLEV2(ulong HandleAddr, ulong HandleValue, int HandleType, ulong appDomainPtr, IntPtr token);

        private int VisitHandleV2(ulong addr, ulong obj, int hndType, ulong appDomain, IntPtr unused)
        {
            Debug.Assert(unused == IntPtr.Zero);

            // V2 cannot actually get the ref count from a handle.  We'll always report the RefCount as
            // 1 in this case so the user will treat this as a strong handle (which the majority of COM
            // handles are).
            uint refCnt = 0;
            if (hndType == (uint)HandleType.RefCount)
                refCnt = 1;

            return AddHandle(addr, obj, hndType, refCnt, 0, appDomain);
        }

        public int AddHandle(ulong addr, ulong obj, int hndType, uint refCnt, uint dependentTarget, ulong appDomain)
        {
            ulong mt;
            ulong cmt;

            // If we fail to get the MT of this object, just skip it and keep going
            if (!GetMethodTables(obj, out mt, out cmt))
                return _max-- > 0 ? 1 : 0;

            ClrHandle handle = new ClrHandle();
            handle.Address = addr;
            handle.Object = obj;
            handle.Type = _heap.GetObjectType(obj);
            handle.HandleType = (HandleType)hndType;
            handle.RefCount = refCnt;
            handle.AppDomain = _runtime.GetAppDomainByAddress(appDomain);
            handle.DependentTarget = dependentTarget;

            if (dependentTarget != 0)
                handle.DependentType = _heap.GetObjectType(dependentTarget);

            Handles.Add(handle);
            handle = handle.GetInteriorHandle();
            if (handle != null)
                Handles.Add(handle);

            // Stop if we have too many handles (likely infinite loop in dac due to
            // inconsistent data).
            return _max-- > 0 ? 1 : 0;
        }

        private bool GetMethodTables(ulong obj, out ulong mt, out ulong cmt)
        {
            mt = 0;
            cmt = 0;

            byte[] data = new byte[IntPtr.Size * 3];        // TODO assumes bitness same as dump
            int read = 0;
            if (!_runtime.ReadMemory(obj, data, data.Length, out read) || read != data.Length)
                return false;

            if (IntPtr.Size == 4)
                mt = BitConverter.ToUInt32(data, 0);
            else
                mt = BitConverter.ToUInt64(data, 0);

            if (mt == _runtime.ArrayMethodTable)
            {
                if (IntPtr.Size == 4)
                    cmt = BitConverter.ToUInt32(data, 2 * IntPtr.Size);
                else
                    cmt = BitConverter.ToUInt64(data, 2 * IntPtr.Size);
            }

            return true;
        }

        private static void FunctionPointerToByteArray(IntPtr functionPtr, byte[] request, int start)
        {
            long ptr = functionPtr.ToInt64();

            for (int i = start; i < start + sizeof(ulong); ++i)
            {
                request[i] = (byte)ptr;
                ptr >>= 8;
            }
        }
        #endregion
    }

    internal class NativeMethods
    {
        public static bool LoadNative(string dllName)
        {
            return LoadLibrary(dllName) != IntPtr.Zero;
        }

        private const string Kernel32LibraryName = "kernel32.dll";

        public const uint FILE_MAP_READ = 4;

        // Call CloseHandle to clean up.
        [DllImport(Kernel32LibraryName, SetLastError = true)]
        public static extern SafeWin32Handle CreateFileMapping(
           SafeFileHandle hFile,
           IntPtr lpFileMappingAttributes, PageProtection flProtect, uint dwMaximumSizeHigh,
           uint dwMaximumSizeLow, string lpName);

        [DllImport(Kernel32LibraryName, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnmapViewOfFile(IntPtr baseAddress);


        [DllImport(Kernel32LibraryName, SetLastError = true)]
        public static extern SafeMapViewHandle MapViewOfFile(SafeWin32Handle hFileMappingObject, uint
           dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow,
           IntPtr dwNumberOfBytesToMap);

        [DllImportAttribute(Kernel32LibraryName)]
        public static extern void RtlMoveMemory(IntPtr destination, IntPtr source, IntPtr numberBytes);

        [DllImport(Kernel32LibraryName, SetLastError = true, PreserveSig = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImportAttribute(Kernel32LibraryName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);

        public static IntPtr LoadLibrary(string lpFileName)
        {
            return LoadLibraryEx(lpFileName, 0, LoadLibraryFlags.NoFlags);
        }

        [DllImportAttribute(Kernel32LibraryName, SetLastError = true)]
        public static extern IntPtr LoadLibraryEx(String fileName, int hFile, LoadLibraryFlags dwFlags);

        [Flags]
        public enum LoadLibraryFlags : uint
        {
            NoFlags = 0x00000000,
            DontResolveDllReferences = 0x00000001,
            LoadIgnoreCodeAuthzLevel = 0x00000010,
            LoadLibraryAsDatafile = 0x00000002,
            LoadLibraryAsDatafileExclusive = 0x00000040,
            LoadLibraryAsImageResource = 0x00000020,
            LoadWithAlteredSearchPath = 0x00000008
        }


        [Flags]
        public enum PageProtection : uint
        {
            NoAccess = 0x01,
            Readonly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            Guard = 0x100,
            NoCache = 0x200,
            WriteCombine = 0x400,
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool isWow64);

        [DllImport("version.dll")]
        internal static extern bool GetFileVersionInfo(string sFileName, int handle, int size, byte[] infoBuffer);

        [DllImport("version.dll")]
        internal static extern int GetFileVersionInfoSize(string sFileName, out int handle);

        [DllImport("version.dll")]
        internal static extern bool VerQueryValue(byte[] pBlock, string pSubBlock, out IntPtr val, out int len);

        private const int VS_FIXEDFILEINFO_size = 0x34;
        public static short IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR = 14;

#if !V2_SUPPORT
        [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
#endif
        [DllImport("dbgeng.dll")]
        internal static extern uint DebugCreate(ref Guid InterfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object Interface);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int CreateDacInstance([In, ComAliasName("REFIID")] ref Guid riid,
                                       [In, MarshalAs(UnmanagedType.Interface)] IDacDataTarget data,
                                       [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppObj);


        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);




#if !V2_SUPPORT
        [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
#endif
        [DllImport("dbghelp.dll")]
        internal static extern IntPtr ImageDirectoryEntryToData(IntPtr mapping, bool mappedAsImage, short directoryEntry, out uint size);

#if !V2_SUPPORT
        [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
#endif
        [DllImport("dbghelp.dll")]
        public static extern IntPtr ImageRvaToVa(IntPtr mapping, IntPtr baseAddr, uint rva, IntPtr lastRvaSection);

#if !V2_SUPPORT
        [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
#endif
        [DllImport("dbghelp.dll")]
        public static extern IntPtr ImageNtHeader(IntPtr imageBase);

        internal static bool IsEqualFileVersion(string file, VersionInfo version)
        {
            int major, minor, revision, patch;
            if (!GetFileVersion(file, out major, out minor, out revision, out patch))
                return false;

            return major == version.Major && minor == version.Minor && revision == version.Revision && patch == version.Patch;
        }


        internal static bool GetFileVersion(string dll, out int major, out int minor, out int revision, out int patch)
        {
            major = minor = revision = patch = 0;

            int handle;
            int len = GetFileVersionInfoSize(dll, out handle);

            if (len <= 0)
                return false;

            byte[] data = new byte[len];
            if (!GetFileVersionInfo(dll, handle, len, data))
                return false;

            IntPtr ptr;
            if (!VerQueryValue(data, "\\", out ptr, out len))
            {
                return false;
            }


            byte[] vsFixedInfo = new byte[len];
            Marshal.Copy(ptr, vsFixedInfo, 0, len);

            minor = (ushort)Marshal.ReadInt16(vsFixedInfo, 8);
            major = (ushort)Marshal.ReadInt16(vsFixedInfo, 10);
            patch = (ushort)Marshal.ReadInt16(vsFixedInfo, 12);
            revision = (ushort)Marshal.ReadInt16(vsFixedInfo, 14);

            return true;
        }

        internal static bool TryGetWow64(IntPtr proc, out bool result)
        {
            if (Environment.OSVersion.Version.Major > 5 ||
                (Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1))
            {
                return IsWow64Process(proc, out result);
            }
            else
            {
                result = false;
                return false;
            }
        }
    }


    internal sealed class SafeWin32Handle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeWin32Handle() : base(true) { }

        public SafeWin32Handle(IntPtr handle)
            : this(handle, true)
        {
        }

        public SafeWin32Handle(IntPtr handle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }

    internal sealed class SafeMapViewHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeMapViewHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.UnmapViewOfFile(handle);
        }

        // This is technically equivalent to DangerousGetHandle, but it's safer for file
        // mappings. In file mappings, the "handle" is actually a base address that needs
        // to be used in computations and RVAs.
        // So provide a safer accessor method.
        public IntPtr BaseAddress
        {
            get
            {
                return handle;
            }
        }
    }

    internal sealed class SafeLoadLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeLoadLibraryHandle() : base(true) { }
        public SafeLoadLibraryHandle(IntPtr handle)
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.FreeLibrary(handle);
        }

        // This is technically equivalent to DangerousGetHandle, but it's safer for loaded
        // libraries where the HMODULE is also the base address the module is loaded at.
        public IntPtr BaseAddress
        {
            get
            {
                return handle;
            }
        }
    }
}
