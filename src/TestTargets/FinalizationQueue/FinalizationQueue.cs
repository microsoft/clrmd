using System;
using System.Collections.Generic;
using System.Threading;

public class FinalizationQueueTarget
{
    public const int ObjectsCountA = 42;
    public const int ObjectsCountB = 13;
    public const int ObjectsCountC = 25;

    private static readonly ICollection<object> _objects = new List<object>();
    private static readonly ManualResetEvent _waitForBlock = new ManualResetEvent(false);

    public static void Main(params string[] args)
    {
        BlockQueue();
        do
        {
            GC.Collect();
        } while (!_waitForBlock.WaitOne(250));

        CreateA();
        GC.Collect();
        GC.Collect();

        CreateB();
        CreateC();

        throw new Exception();
    }

    private static void BlockQueue()
    {
        // Pause the finalizer queue
        new DieHard(_waitForBlock);
    }

    private static void CreateA()
    {
        for (var i = 0; i < ObjectsCountA; i++)
            Console.WriteLine(new SampleA());
    }

    private static void CreateB()
    {
        for (var i = 0; i < ObjectsCountB; i++)
            _objects.Add(new SampleB());
    }

    private static void CreateC()
    {
        for (var i = 0; i < ObjectsCountC; i++)
            Console.WriteLine(new SampleC());
    }
}

public class DieHard
{
    private static readonly WaitHandle _handle = new ManualResetEvent(false);
    private readonly ManualResetEvent _evt;

    public DieHard(ManualResetEvent evt) { _evt = evt; }


    ~DieHard()
    {
        Console.WriteLine("FQ blocked");
        _evt.Set();
        _handle.WaitOne();
    }
}

public class SampleA
{
    ~SampleA()
    {
        Console.WriteLine("SampleA: {0}", GetHashCode());
    }
}

public class SampleB
{
    ~SampleB()
    {
        Console.WriteLine("SampleB: {0}", GetHashCode());
    }
}

public class SampleC
{
    ~SampleC()
    {
        Console.WriteLine("SampleC: {0}", GetHashCode());
    }
}
