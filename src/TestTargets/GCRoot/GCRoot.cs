// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable 0162
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

//                                              object
//                                            /
// SingleRef -- object[] -- DoubleRef -- TripleRef -- SingleRef -- TargetType
//                              \           / \           /
//                                SingleRef     SingleRef
class GCRootTarget
{
    static object TheRoot;
    static ConditionalWeakTable<SingleRef, TargetType> _dependent = new ConditionalWeakTable<SingleRef, TargetType>();

    public static void Main(string[] args)
    {
        TargetType target = new TargetType();
        SingleRef s = new SingleRef();
        DoubleRef d = new DoubleRef();
        TripleRef t = new TripleRef();

        TheRoot = s;

        object[] arr = new object[42];
        s.Item1 = arr;
        arr[27] = d;

        // parallel path.
        d.Item1 = new SingleRef() { Item1 = t };
        d.Item2 = t;

        s = new SingleRef();

        t.Item1 = new SingleRef() { Item1 = s };
        t.Item2 = s;
        t.Item3 = new object(); // dead path

        _dependent.Add(s, target);

        // Create explicit strong roots so that EnumerateRootPaths finds multiple
        // independent GC roots reaching the target.  On .NET 10+ statics live in
        // a single Object[] handle and stack roots may not include locals, so
        // without these handles only 1 root path would exist.
        AllocRoots(target, s, t);

        throw new Exception();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AllocRoots(TargetType target, SingleRef s, TripleRef t)
    {
        GCHandle.Alloc(target);  // root → target (direct)
        GCHandle.Alloc(s);       // root → s → target (via dependent handle)
        GCHandle.Alloc(t);       // root → t → ... → s → target
    }
}


class SingleRef
{
    public object Item1;
}


class DoubleRef
{
    public object Item1;
    public object Item2;
}


class TripleRef
{
    public object Item1;
    public object Item2;
    public object Item3;
}

class TargetType
{

}
