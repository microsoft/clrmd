#pragma warning disable 0162
using System;
using System.Runtime.CompilerServices;

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
        //s.Item1 = target;
        throw new Exception();
        GC.KeepAlive(target);
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
