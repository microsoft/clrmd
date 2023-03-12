// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
