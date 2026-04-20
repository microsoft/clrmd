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
//      VirtualAlloc(MEM_RESERVE, PAGE_NOACCESS) BEFORE touching coreclr,
//      so subsequent CLR heap allocations land in the upper 2 GB.
//   2. Loading hostfxr.dll via the standard nethost discovery path, then
//      invoking hostfxr_run_app to run the managed target DLL in-process.
//   3. Relying on CoreCLR's createdump integration (DOTNET_DbgEnableMiniDump
//      env vars set by the parent test process) to capture a dump when the
//      managed target throws.
//
// Usage:  HighBitHost.exe <target.dll> [args...]

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string>
#include <vector>

#include <nethost.h>
#include <hostfxr.h>

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
}

int wmain(int argc, wchar_t** argv)
{
    // Force unbuffered stdout/stderr so DumpGenerator on the .NET side always
    // captures our diagnostic lines, even if the managed app never returns.
    setvbuf(stdout, nullptr, _IONBF, 0);
    setvbuf(stderr, nullptr, _IONBF, 0);

    if (argc < 2)
    {
        fprintf(stderr, "Usage: HighBitHost.exe <target.dll> [args...]\n");
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

    std::wstring hostfxrPath;
    HostFxrEntryPoints ep;
    if (!LoadHostFxr(hostfxrPath, ep))
        return 3;

    wprintf(L"HighBitHost: loaded hostfxr from %ls\n", hostfxrPath.c_str());
    wprintf(L"HighBitHost: running %ls\n", argv[1]);

    // Forward argv[1..] to hostfxr: it treats argv[0] as the managed DLL and
    // the rest as arguments to the managed entry point, matching the
    // behaviour of `dotnet <target.dll> [args...]`.
    std::vector<const wchar_t*> forwarded;
    forwarded.reserve(argc - 1);
    for (int i = 1; i < argc; ++i)
        forwarded.push_back(argv[i]);

    int rc = RunManagedApp(ep, static_cast<int>(forwarded.size()), forwarded.data());

    // If the managed app threw, createdump has already written the dump file
    // by the time we get here (or the process was terminated). Propagate
    // the exit code so DumpGenerator's failure diagnostics line up.
    return rc;
}
