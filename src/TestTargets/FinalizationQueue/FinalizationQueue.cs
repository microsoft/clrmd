using System;
using System.Collections.Generic;
using System.Threading;

public class FinalizationQueueTarget
{
    public const int ObjectsCountA = 42;
    public const int ObjectsCountB = 13;
    public const int ObjectsCountC = 25;

    private static readonly ICollection<object> _objects = new List<object>();

    public static void Main(params string[] args)
    {
        BlockQueue();
        GC.Collect();

        CreateA();
        GC.Collect();

        CreateB();
        CreateC();

        throw new Exception();
    }
    
    private static void BlockQueue()
    {
        // Pause the finalizer queue
        Console.WriteLine(new DieHard());
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

    ~DieHard()
    {
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