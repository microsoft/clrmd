using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// GCHandle \
//            DirectTarget -- IndirectTarget
// GCHandle /
class GCRootTarget
{
    private static void Main()
    {
        Alloc();
        throw new Exception();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Alloc()
    {
        DirectTarget source = new DirectTarget
        {
            Item = new IndirectTarget()
        };

        GCHandle.Alloc(source);
        GCHandle.Alloc(source);
    }
}

class DirectTarget
{
    public object Item;
}

class IndirectTarget
{
}
