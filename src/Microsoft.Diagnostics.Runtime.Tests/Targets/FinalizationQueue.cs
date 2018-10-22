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
    for (var i = 0; i < ObjectsCountA; i++)
      _objects.Add(new DieFastA());
    
    Console.WriteLine(new DieHard());
    GC.Collect();
    
    for (var i = 0; i < ObjectsCountB; i++)
      Console.WriteLine(new DieFastB());
    GC.Collect();
    
    throw new Exception();
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

public class DieFastA
{
  ~DieFastA()
  {
    Console.WriteLine(GetHashCode());
  }
}

public class DieFastB
{
  ~DieFastB()
  {
    Console.WriteLine(GetHashCode());
  }
}