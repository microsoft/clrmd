// See https://aka.ms/new-console-template for more information
using System.Diagnostics;

Console.WriteLine(Process.GetCurrentProcess().Id);
while (true)
    Thread.Sleep(1000);
