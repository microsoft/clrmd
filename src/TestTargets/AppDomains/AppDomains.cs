// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using System.Threading;

class Program
{
    static Foo s_foo = new Foo();
    static void Main(string[] args)
    {
        string codebase = Assembly.GetExecutingAssembly().CodeBase;

        if (codebase.StartsWith("file://"))
            codebase = codebase.Substring(8).Replace('/', '\\');

        SharedStaticTest.Value = 2;

        AppDomain domain = AppDomain.CreateDomain("Second AppDomain");
        domain.ExecuteAssembly(Path.Combine(Path.GetDirectoryName(codebase), "NestedException.exe"));

        while (true)
            Thread.Sleep(250);
    }
}
