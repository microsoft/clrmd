// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using DbgEngExtension;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.DbgEng;

if (args.Length != 1 || !File.Exists(args[0]))
    Exit("Usage:  DbgEngStandalone [dump-file-path]");

// There is a copy of dbgeng.dll in the System32 folder, but that copy of DbgEng is very old
// and has several features compiled out of it for security reasons.  We can use that version
// but it limits functionality.  Instead, you can set a path (or an EnvVariable) to contain
// a locally installed copy of DbgEng.  This demo will still work if we default to the
// system32 version, but it works better with an installed version of DbgEng.
const string ExpectedDbgEngInstallPath = @"d:\amd64";

// This isn't set by anything, but you can set it yourself for this demo.
const string ExpectedDbgEngPathEnvVariable = "DbgEngPath";

string? dbgengPath = FindDbgEngPath();

// IDebugClient.Create creates a COM wrapper object.  You can cast this object to dbgeng interfaces.
using IDisposable dbgeng = IDebugClient.Create(dbgengPath);

// All DbgEng interfaces are simply
IDebugClient client = (IDebugClient)dbgeng;
IDebugControl control = (IDebugControl)dbgeng;

// Most functions return an HRESULT int, but ClrMD has an 'HResult' helper.
HResult hr = client.OpenDumpFile(args[0]);
CheckHResult(hr, $"Failed to load {args[0]}.");

// You have to wait for dbgeng to attach to the dump file
hr = control.WaitForEvent(TimeSpan.MaxValue);
CheckHResult(hr, "WaitForEvent unexpectedly failed.");

// DbgEngOutputHolder will capture output of the debugger.  Here we will print dbgeng
// messages in Yellow with its output mask in Green as an example:
using (DbgEngOutputHolder output = new(client, DEBUG_OUTPUT.ALL))
{
    output.OutputReceived += (text, flags) => {
        ConsoleColor oldColor = Console.ForegroundColor;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"[{flags}] ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(text);
        Console.Out.Flush();

        Console.ForegroundColor = oldColor;
    };

    hr = control.Execute(DEBUG_OUTCTL.THIS_CLIENT, ".chain", DEBUG_EXECUTE.DEFAULT);
}

// Running this same command without capturing the output results in no output hitting the console:
control.Execute(DEBUG_OUTCTL.THIS_CLIENT, ".chain", DEBUG_EXECUTE.DEFAULT);


// We can create an instance of ClrMD with Microsoft.Diagnostics.Runtime.Utilities too:
DataTarget dt = DbgEngIDataReader.CreateDataTarget(dbgeng);

Console.WriteLine("CLR runtimes in this dump file:");
foreach (ClrRuntime runtime in dt.ClrVersions.Select(clr => clr.CreateRuntime()))
{
    Console.WriteLine($"    {runtime.ClrInfo}:");

    Console.WriteLine("        First 10 objects:");
    foreach (ClrObject obj in runtime.Heap.EnumerateObjects().Take(10))
        Console.WriteLine($"            {obj}");
}

// Let's re-use our DbgEngExtension as an example.  We don't redirect Console.WriteLine because
// we are a standalone application and DbgEng isn't writing to console.  Note that also putting
// a "using" statement here is optional.  By having "using" here, we fully QueryInterface from
// scratch, build ClrMD from scratch, and tear it down in Dispose.  If we don't dispose any
// DbgEngCommand subclass, then we cache the IDebug* interfaces and ClrMD for performance.
using (MHeap heapExtension = new(dbgeng, redirectConsoleOutput: false))
{
    heapExtension.Run(statOnly: false);

    // We can also initialize DbgEngCommand classes with another class.  By initializing MAddress
    // with heapExtension, we re-use and share all DbgEng interfaces and ClrMD classes.  This
    // greatly improves performance.  When heapExtension is disposed at the end of this using
    // statement, dbgeng/ClrMD will be cleaned up for maddress too.
    MAddress maddress = new(heapExtension);
    maddress.PrintMemorySummary(printAllMemory: true, showImageTable: true, includeReserveMemory: false, tagReserveMemoryHeuristically: false);
}

// End of Demo.



// Helper Methods
static void CheckHResult(HResult hr, string msg)
{
    if (!hr)
        Exit(msg, hr);
}

static void Exit(string message, int exitCode = -1)
{
    Console.Error.WriteLine(message);
    Environment.Exit(exitCode);
}

static string? FindDbgEngPath()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        throw new NotSupportedException($"DbgEng only exists for Windows.");

    if (CheckOneFolderForDbgEng(ExpectedDbgEngInstallPath))
        return ExpectedDbgEngInstallPath;

    string? dbgEngEnv = Environment.GetEnvironmentVariable(ExpectedDbgEngPathEnvVariable);
    if (CheckOneFolderForDbgEng(dbgEngEnv))
        return dbgEngEnv!;

    string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
    if (CheckOneFolderForDbgEng(system32))
        return system32;

    return null;
}

static bool CheckOneFolderForDbgEng(string? directory)
{
    if (!Directory.Exists(directory))
        return false;

    string path = Path.Combine(directory, "dbgeng.dll");
    return File.Exists(path);
}