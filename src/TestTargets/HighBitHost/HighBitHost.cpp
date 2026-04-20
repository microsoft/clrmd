// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// HighBitHost.exe
// ---------------
// Native launcher used by ClrMD's test harness to produce crash dumps where
// the managed heap sits in the high half of the 32-bit address space
// (addresses with bit 31 set, i.e. >= 0x80000000).
//
// Normal x64-Windows-hosted test runs rarely place managed objects above
// 0x80000000, so ClrMD's 3-GB-range code paths (sign-extension handling in
// legacy DACs, ClrDataAddress conversions, minidump ULONG64 handling)
// effectively go untested. This host fixes that by:
//
//   1. Reserving every MEM_FREE region below 0x80000000 with
//      VirtualAlloc(MEM_RESERVE, PAGE_NOACCESS) BEFORE touching any CLR,
//      so subsequent CLR heap allocations land in the upper 2 GB.
//   2. Loading the requested runtime (Core via hostfxr, Framework v4 via
//      mscoree) and invoking the managed target's entry point in-process.
//   3. Relying on the runtime's crash-dump integration (DOTNET_DbgEnableMiniDump
//      for Core, DbgEng from the parent for Framework) to capture a dump
//      when the managed target throws.
//
// Usage:
//   HighBitHost.exe [--mode=core|framework] <target> [args...]
//     core      (default) runs <target.dll> via hostfxr_run_app
//     framework runs <target.exe> via ICorRuntimeHost + _AppDomain::ExecuteAssembly

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string>
#include <vector>

#include <nethost.h>
#include <hostfxr.h>

// Framework hosting.
#include <metahost.h>
#include <mscoree.h>
#include <comdef.h>

#pragma comment(lib, "mscoree.lib")

// Pull in _AppDomain from mscorlib.tlb. Paths differ between 32- and 64-bit OS;
// the standard system32/SysWOW64 mscorlib.tlb is the canonical location.
#import "libid:BED7F4EA-1A96-11D2-8F08-00A0C9A6186D" \
    raw_interfaces_only                             \
    high_property_prefixes("_get","_put","_putref") \
    rename("ReportEvent", "InteropServices_ReportEvent") \
    auto_rename
using namespace mscorlib;

namespace
{
    constexpr uintptr_t kHighBitBoundary = 0x80000000u;

    struct ReservationSummary
    {
        uint64_t  bytesReserved       = 0;
        uint64_t  largestFreeHoleSize = 0;
        uintptr_t largestFreeHoleBase = 0;
        uintptr_t largestFreeHoleEnd  = 0;
        unsigned  regionsReserved     = 0;
        unsigned  regionsFailed       = 0;
    };

    // Walks the virtual address space from 0x10000 up to kHighBitBoundary,
    // reserving every MEM_FREE region with PAGE_NOACCESS so that subsequent
    // allocations (including CLR heap segments) are pushed into the upper
    // 2 GB of a LARGEADDRESSAWARE 32-bit process.
    //
    // We intentionally tolerate failures: ASLR and loader activity will have
    // placed a handful of small regions in low memory before main() runs, and
    // we can't dislodge them. The test still succeeds as long as enough high
    // memory is left free for the CLR to find.
    ReservationSummary ReserveLowAddressSpace()
    {
        ReservationSummary s;

        SYSTEM_INFO si;
        GetSystemInfo(&si);
        const uintptr_t allocGran = static_cast<uintptr_t>(si.dwAllocationGranularity);

        uintptr_t addr = 0x10000u;
        while (addr < kHighBitBoundary)
        {
            MEMORY_BASIC_INFORMATION mbi = {};
            SIZE_T got = VirtualQuery(reinterpret_cast<LPCVOID>(addr),
                                      &mbi, sizeof(mbi));
            if (got == 0)
                break;

            uintptr_t regionBase = reinterpret_cast<uintptr_t>(mbi.BaseAddress);
            uintptr_t regionEnd  = regionBase + static_cast<uintptr_t>(mbi.RegionSize);
            if (regionEnd > kHighBitBoundary)
                regionEnd = kHighBitBoundary;

            if (mbi.State == MEM_FREE && regionEnd > regionBase)
            {
                // VirtualAlloc(MEM_RESERVE) requires the base to be on an allocation
                // granularity boundary (usually 64 KB). VirtualQuery can report a
                // MEM_FREE region that starts on a smaller page boundary, so round up.
                uintptr_t alignedBase = (regionBase + allocGran - 1) & ~(allocGran - 1);
                if (alignedBase < regionEnd)
                {
                    SIZE_T reserveSize = static_cast<SIZE_T>(regionEnd - alignedBase);
                    LPVOID got2 = VirtualAlloc(reinterpret_cast<LPVOID>(alignedBase),
                                               reserveSize,
                                               MEM_RESERVE,
                                               PAGE_NOACCESS);
                    if (got2 != nullptr)
                    {
                        s.bytesReserved += reserveSize;
                        s.regionsReserved++;
                    }
                    else
                    {
                        s.regionsFailed++;
                        uint64_t size = regionEnd - alignedBase;
                        if (size > s.largestFreeHoleSize)
                        {
                            s.largestFreeHoleSize = size;
                            s.largestFreeHoleBase = alignedBase;
                            s.largestFreeHoleEnd  = regionEnd;
                        }
                    }
                }
            }

            uintptr_t next = regionBase + static_cast<uintptr_t>(mbi.RegionSize);
            if (next <= addr)
                break;  // paranoia: prevent infinite loop on a weird region
            addr = next;
        }

        return s;
    }

    struct HostFxrEntryPoints
    {
        hostfxr_initialize_for_dotnet_command_line_fn init  = nullptr;
        hostfxr_run_app_fn                            run   = nullptr;
        hostfxr_close_fn                              close = nullptr;
    };

    bool LoadHostFxr(std::wstring& hostfxrPathOut, HostFxrEntryPoints& epOut)
    {
        wchar_t buf[MAX_PATH];
        size_t  bufSize = sizeof(buf) / sizeof(buf[0]);
        int rc = get_hostfxr_path(buf, &bufSize, nullptr);
        if (rc != 0)
        {
            fprintf(stderr, "HighBitHost: get_hostfxr_path failed (0x%08X)\n", (unsigned)rc);
            return false;
        }

        hostfxrPathOut.assign(buf);

        HMODULE hostfxr = LoadLibraryW(buf);
        if (hostfxr == nullptr)
        {
            fprintf(stderr, "HighBitHost: LoadLibraryW(hostfxr) failed (%lu)\n", GetLastError());
            return false;
        }

        epOut.init  = reinterpret_cast<hostfxr_initialize_for_dotnet_command_line_fn>(
            GetProcAddress(hostfxr, "hostfxr_initialize_for_dotnet_command_line"));
        epOut.run   = reinterpret_cast<hostfxr_run_app_fn>(
            GetProcAddress(hostfxr, "hostfxr_run_app"));
        epOut.close = reinterpret_cast<hostfxr_close_fn>(
            GetProcAddress(hostfxr, "hostfxr_close"));

        if (!epOut.init || !epOut.run || !epOut.close)
        {
            fprintf(stderr, "HighBitHost: failed to resolve hostfxr entry points\n");
            return false;
        }

        return true;
    }

    int RunManagedApp(const HostFxrEntryPoints& ep,
                      int argc,
                      const wchar_t** argv)
    {
        hostfxr_handle handle = nullptr;
        int rc = ep.init(argc, argv, nullptr, &handle);
        if (rc != 0 || handle == nullptr)
        {
            fprintf(stderr,
                    "HighBitHost: hostfxr_initialize_for_dotnet_command_line failed (0x%08X)\n",
                    (unsigned)rc);
            if (handle != nullptr)
                ep.close(handle);
            return rc != 0 ? rc : -1;
        }

        int appRc = ep.run(handle);
        ep.close(handle);
        return appRc;
    }

    // Hosts desktop .NET Framework v4 via mscoree and runs the managed target .exe's
    // entry point. Any unhandled managed exception propagates via DbgEng-driven
    // dump capture (set up by DumpGenerator on the parent side), so we don't
    // install our own crash handler here.
    int RunFrameworkApp(const wchar_t* targetExePath)
    {
        ICLRMetaHost* pMetaHost = nullptr;
        HRESULT hr = CLRCreateInstance(CLSID_CLRMetaHost, IID_PPV_ARGS(&pMetaHost));
        if (FAILED(hr) || pMetaHost == nullptr)
        {
            fprintf(stderr, "HighBitHost(framework): CLRCreateInstance failed (0x%08X)\n", (unsigned)hr);
            return 10;
        }

        ICLRRuntimeInfo* pRuntimeInfo = nullptr;
        hr = pMetaHost->GetRuntime(L"v4.0.30319", IID_PPV_ARGS(&pRuntimeInfo));
        if (FAILED(hr) || pRuntimeInfo == nullptr)
        {
            fprintf(stderr, "HighBitHost(framework): GetRuntime(v4.0.30319) failed (0x%08X)\n", (unsigned)hr);
            pMetaHost->Release();
            return 11;
        }

        ICorRuntimeHost* pHost = nullptr;
        hr = pRuntimeInfo->GetInterface(CLSID_CorRuntimeHost, IID_PPV_ARGS(&pHost));
        if (FAILED(hr) || pHost == nullptr)
        {
            fprintf(stderr, "HighBitHost(framework): GetInterface(CorRuntimeHost) failed (0x%08X)\n", (unsigned)hr);
            pRuntimeInfo->Release();
            pMetaHost->Release();
            return 12;
        }

        hr = pHost->Start();
        if (FAILED(hr))
        {
            fprintf(stderr, "HighBitHost(framework): ICorRuntimeHost::Start failed (0x%08X)\n", (unsigned)hr);
            pHost->Release();
            pRuntimeInfo->Release();
            pMetaHost->Release();
            return 13;
        }

        IUnknown* pAppDomainThunk = nullptr;
        hr = pHost->GetDefaultDomain(&pAppDomainThunk);
        if (FAILED(hr) || pAppDomainThunk == nullptr)
        {
            fprintf(stderr, "HighBitHost(framework): GetDefaultDomain failed (0x%08X)\n", (unsigned)hr);
            pHost->Stop();
            pHost->Release();
            pRuntimeInfo->Release();
            pMetaHost->Release();
            return 14;
        }

        _AppDomain* pDefaultDomain = nullptr;
        hr = pAppDomainThunk->QueryInterface(__uuidof(_AppDomain), (void**)&pDefaultDomain);
        pAppDomainThunk->Release();
        if (FAILED(hr) || pDefaultDomain == nullptr)
        {
            fprintf(stderr, "HighBitHost(framework): QI(_AppDomain) failed (0x%08X)\n", (unsigned)hr);
            pHost->Stop();
            pHost->Release();
            pRuntimeInfo->Release();
            pMetaHost->Release();
            return 15;
        }

        _bstr_t assemblyPath(targetExePath);
        long retVal = 0;

        // ExecuteAssembly_2(BSTR AssemblyFile, long* pRetVal) runs the static Main
        // of the assembly's entry point in the default AppDomain. If Main throws
        // unhandled, CLR's hosted policy CATCHES the exception and returns it as
        // an HRESULT (typically 0x80131500-range) instead of letting SEH propagate.
        // That's bad for us — the parent DbgEng is gated on the 0xe0434352 CLR
        // managed-exception event to capture the dump. So we check the result and
        // re-raise the CLR exception in-process while the managed heap is still
        // live. Call RaiseException BEFORE tearing down the CLR so the captured
        // dump has an intact heap for the ClrMD tests to inspect.
        hr = pDefaultDomain->ExecuteAssembly_2(assemblyPath, &retVal);

        if (FAILED(hr))
        {
            fprintf(stderr, "HighBitHost(framework): ExecuteAssembly returned 0x%08X; re-raising as CLR exception for dump capture\n", (unsigned)hr);
            fflush(stderr);
            ULONG_PTR args[1] = { static_cast<ULONG_PTR>(static_cast<unsigned>(hr)) };
            // 0xe0434352 = 'CCR\xe0' = CLR managed exception. Matches what the
            // parent DbgEng event hook in DumpGenerator filters on.
            RaiseException(0xe0434352u, 0, 1, args);
        }

        pDefaultDomain->Release();
        pHost->Stop();
        pHost->Release();
        pRuntimeInfo->Release();
        pMetaHost->Release();

        return (int)retVal;
    }
}

int wmain(int argc, wchar_t** argv)
{
    // Force unbuffered stdout/stderr so DumpGenerator on the .NET side always
    // captures our diagnostic lines, even if the managed app never returns.
    setvbuf(stdout, nullptr, _IONBF, 0);
    setvbuf(stderr, nullptr, _IONBF, 0);

    // Parse --mode={core,framework} (optional, default core). Remaining args are
    // forwarded to the selected host.
    enum class Mode { Core, Framework };
    Mode mode = Mode::Core;

    int firstPositional = 1;
    if (argc >= 2 && argv[1] != nullptr)
    {
        const wchar_t* a = argv[1];
        if (_wcsicmp(a, L"--mode=core") == 0)
        {
            mode = Mode::Core;
            firstPositional = 2;
        }
        else if (_wcsicmp(a, L"--mode=framework") == 0)
        {
            mode = Mode::Framework;
            firstPositional = 2;
        }
    }

    if (argc - firstPositional < 1)
    {
        fprintf(stderr, "Usage: HighBitHost.exe [--mode=core|framework] <target> [args...]\n");
        return 2;
    }

    ReservationSummary summary = ReserveLowAddressSpace();
    printf("HighBitHost: reserved 0x%llX bytes below 0x%zX across %u regions (%u failed)\n",
           (unsigned long long)summary.bytesReserved,
           (size_t)kHighBitBoundary,
           summary.regionsReserved,
           summary.regionsFailed);
    if (summary.largestFreeHoleSize != 0)
    {
        printf("HighBitHost: largest remaining free hole: 0x%zX..0x%zX (0x%llX bytes)\n",
               (size_t)summary.largestFreeHoleBase,
               (size_t)summary.largestFreeHoleEnd,
               (unsigned long long)summary.largestFreeHoleSize);
    }

    if (mode == Mode::Framework)
    {
        // Framework path: initialize COM (STA matches the default for managed
        // [STAThread] Main entry points), then host CLR v4 in-process.
        HRESULT hrCo = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        if (FAILED(hrCo) && hrCo != RPC_E_CHANGED_MODE)
        {
            fprintf(stderr, "HighBitHost(framework): CoInitializeEx failed (0x%08X)\n", (unsigned)hrCo);
            return 4;
        }

        wprintf(L"HighBitHost(framework): running %ls\n", argv[firstPositional]);
        int rc = RunFrameworkApp(argv[firstPositional]);

        if (SUCCEEDED(hrCo))
            CoUninitialize();
        return rc;
    }

    // Core path.
    std::wstring hostfxrPath;
    HostFxrEntryPoints ep;
    if (!LoadHostFxr(hostfxrPath, ep))
        return 3;

    wprintf(L"HighBitHost(core): loaded hostfxr from %ls\n", hostfxrPath.c_str());
    wprintf(L"HighBitHost(core): running %ls\n", argv[firstPositional]);

    // Forward positional args [firstPositional..) to hostfxr: it treats the
    // first as the managed DLL and the rest as arguments, matching the
    // behavior of `dotnet <target.dll> [args...]`.
    std::vector<const wchar_t*> forwarded;
    forwarded.reserve(argc - firstPositional);
    for (int i = firstPositional; i < argc; ++i)
        forwarded.push_back(argv[i]);

    int rc = RunManagedApp(ep, static_cast<int>(forwarded.size()), forwarded.data());

    // If the managed app threw, createdump has already written the dump file
    // by the time we get here (or the process was terminated). Propagate
    // the exit code so DumpGenerator's failure diagnostics line up.
    return rc;
}
