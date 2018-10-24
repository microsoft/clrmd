using System;
using System.Collections.Generic;
using System.Threading;

public class FinalizationQueueTarget
{
    public const int ObjectsCountA = 42;
    public const int ObjectsCountB = 13;

    private static readonly ICollection<object> _objects = new List<object>();

    public static void Main(params string[] args)
    {
        Test1();
        GC.Collect();
        
        Test2();
        GC.Collect();
        GC.Collect();

        throw new Exception();
    }
    
    private static void Test1()
    {
        // Pause the finalizer queue
        Console.WriteLine(new DieHard());
    }
    
    private static void Test2()
    {
        for (var i = 0; i < ObjectsCountA; i++)
          _objects.Add(new DieFastA());

        for (var i = 0; i < ObjectsCountB; i++)
          Console.WriteLine(new DieFastB());
    }
}

public class DieHard
{
    private static readonly WaitHandle _handle = new ManualResetEvent(false);

    ~DieHard() { _handle.WaitOne(); }
}

public class DieFastA
{
  ~DieFastA() { Console.WriteLine("DieFastA: {0}", GetHashCode()); }
}

public class DieFastB
{
  ~DieFastB() { Console.WriteLine("DieFastB: {0}", GetHashCode()); }
  
}