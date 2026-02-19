#!/usr/bin/env bash
set -e

scriptroot="$( cd -P "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
testproject="$scriptroot/src/Microsoft.Diagnostics.Runtime.Tests/Microsoft.Diagnostics.Runtime.Tests.csproj"
exitcode=0
arch="${1}"
shift 2>/dev/null || true

run_64() {
    echo "=== Running 64-bit tests ==="
    dotnet test "$testproject" --arch x64 "$@" || exitcode=1
}

can_run_32() {
    dotnet --info --arch x86 >/dev/null 2>&1
}

run_32() {
    if ! can_run_32; then
        echo "=== Skipping 32-bit tests (x86 .NET runtime not found) ==="
        return
    fi
    echo "=== Running 32-bit tests ==="
    dotnet test "$testproject" --arch x86 "$@" || exitcode=1
}

case "$arch" in
    ""|"--help")
        if [ -z "$arch" ]; then
            run_64 "$@"
            echo ""
            run_32 "$@"
        else
            echo "Usage: test.sh [arch] [dotnet test args...]"
            echo "  No args: run both 32-bit and 64-bit tests"
            echo "  x86, x32, arm:     run 32-bit tests only"
            echo "  x64, amd64, arm64: run 64-bit tests only"
            exit 1
        fi
        ;;
    x86|x32|arm)
        run_32 "$@"
        ;;
    x64|amd64|arm64)
        run_64 "$@"
        ;;
    *)
        echo "Unknown architecture: $arch"
        echo "Use x86/x32/arm for 32-bit, x64/amd64/arm64 for 64-bit"
        exit 1
        ;;
esac

exit $exitcode