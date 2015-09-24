using System;
using System.IO;

class Program
{
    public static void Main(string[] args)
    {
        Outer();
    }

    private static void Outer()
    {
        Middle();
    }

    private static void Middle()
    {
        Inner();
    }

    private static void Inner()
    {
        try
        {
            throw new FileNotFoundException("FNF Message");
        }
        catch (FileNotFoundException e)
        {
            throw new InvalidOperationException("IOE Message", e);
        }
    }
}
