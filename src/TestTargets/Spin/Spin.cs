using System;
using System.Threading;

class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
            throw new Exception();

        Console.WriteLine();
        Thread.SpinWait(int.MaxValue);
    }
}
