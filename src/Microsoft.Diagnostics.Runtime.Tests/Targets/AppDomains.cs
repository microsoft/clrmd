using System;
using System.IO;
using System.Reflection;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        Foo foo = new Foo();

        string codebase = Assembly.GetExecutingAssembly().CodeBase;

        if (codebase.StartsWith("file://"))
            codebase = codebase.Substring(8).Replace('/', '\\');

        AppDomain domain = AppDomain.CreateDomain("Second AppDomain");

        domain.ExecuteAssembly(Path.Combine(Path.GetDirectoryName(codebase), "NestedException.exe"));

        while (true)
            Thread.Sleep(250);

        GC.KeepAlive(foo);
    }
}