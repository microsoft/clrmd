// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// See https://aka.ms/new-console-template for more information
using System.Diagnostics;

Console.WriteLine(Process.GetCurrentProcess().Id);
while (true)
    Thread.Sleep(1000);
