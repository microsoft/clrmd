using Microsoft.Diagnostics.Runtime.Builders;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;
using System.Collections.Generic;
using static Microsoft.Diagnostics.Runtime.DacInterface.SOSDac13;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    // Helpers to evaluate lazy fields.
    public interface IJitManagerHelpers
    {
        IEnumerable<ClrNativeHeapInfo> EnumerateNativeHeaps(ClrJitManager clrJitManager);
    }

    internal sealed class JitManagerHelpers : IJitManagerHelpers
    {
        private readonly SOSDac _sos;
        private readonly SOSDac13? _sos13;

        public JitManagerHelpers(SOSDac sos, SOSDac13? sos13)
        {
            _sos = sos;
            _sos13 = sos13;
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateNativeHeaps(ClrJitManager jitMgr)
        {
            foreach (var mem in _sos.GetCodeHeapList(jitMgr.Address))
            {
                if (mem.Kind == CodeHeapKind.Loader)
                {
                    List<ClrNativeHeapInfo>? codeLoaderHeaps = null;

                    ClrRuntime runtime = jitMgr.Runtime;
                    uint pointerSize = (uint)runtime.DataTarget.DataReader.PointerSize;
                    HResult hr = RuntimeBuilder.TraverseLoaderHeap(runtime.ClrInfo, _sos, _sos13, mem.Address, LoaderHeapKind.LoaderHeapKindExplicitControl, pointerSize, (address, size, isCurrent) =>
                    {
                        codeLoaderHeaps ??= new(16);
                        codeLoaderHeaps.Add(new ClrNativeHeapInfo(address, RuntimeBuilder.SanitizeSize(size), NativeHeapKind.LoaderCodeHeap, isCurrent != 0));
                    });

                    if (codeLoaderHeaps is not null)
                        foreach (ClrNativeHeapInfo result in codeLoaderHeaps)
                            yield return result;

                    codeLoaderHeaps?.Clear();
                }
                else if (mem.Kind == CodeHeapKind.Host)
                {
                    ulong? size = mem.CurrentAddress >= mem.Address ? mem.CurrentAddress - mem.Address : null;
                    yield return new ClrNativeHeapInfo(mem.Address, size, NativeHeapKind.HostCodeHeap, true);
                }
                else
                {
                    yield return new ClrNativeHeapInfo(mem.Address, null, NativeHeapKind.Unknown, false);
                }
            }
        }
    }
}