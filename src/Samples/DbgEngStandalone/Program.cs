using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.DbgEng;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

if (!File.Exists(args[0]))
    Exit("Usage:  DbgEngStandalone [dump-file-path]");

using (DataTarget tmpDt = DataTarget.LoadDump(args[0]))
{
    using ClrRuntime rt = tmpDt.ClrVersions.Single().CreateRuntime();
    int count = 0;

    rt.Heap.IsObjectCorrupted(0x7f1b69029820, out _);
    rt.Heap.IsObjectCorrupted(0x7f1b69127000, out _);
    rt.Heap.IsObjectCorrupted(0x7f1b69162f68, out _);

    Stopwatch sw = Stopwatch.StartNew();
    foreach (ClrObject obj in rt.Heap.EnumerateObjects())
    {
        if (rt.Heap.IsObjectCorrupted(obj, out ObjectCorruption? corruptedObj))
        {
            Console.WriteLine(corruptedObj);
        }

        if ((++count % 100_000) == 0)
            Console.Title = $"{count:n0} objects verified";
    }

    Console.WriteLine(sw.Elapsed);
}

Environment.Exit(0);

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
IDebugDataSpaces spaces = (IDebugDataSpaces)dbgeng;

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
    output.OutputReceived += (text, flags) =>
    {
        var oldColor = Console.ForegroundColor;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"[{flags}] ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(text);
        Console.Out.Flush();

        Console.ForegroundColor = oldColor;
    };

    hr = control.Execute(DEBUG_OUTCTL.THIS_CLIENT, ".unload sos", DEBUG_EXECUTE.DEFAULT);
    hr = control.Execute(DEBUG_OUTCTL.THIS_CLIENT, ".load C:/git/dotnet/diagnostics/artifacts/bin/Windows_NT.x64.Debug/./sos.dll", DEBUG_EXECUTE.DEFAULT);

    hr = control.Execute(DEBUG_OUTCTL.THIS_CLIENT, ".chain", DEBUG_EXECUTE.DEFAULT);
}

// We can create an instance of ClrMD with Microsoft.Diagnostics.Runtime.Utilities too:
using DataTarget dt = DbgEngIDataReader.CreateDataTarget(dbgeng);
using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
ClrHeap heap = runtime.Heap;
///CorruptHeapAndTest(args[0] + ".txt");
///

foreach (Modification mod in JsonSerializer.Deserialize<Modification[]>(File.ReadAllText(args[0] + ".txt"))!)
{
    if (mod.Address == 0x27FFFAC70)
    {

    }
    var clrMDMod = WriteAndVerifyClrMD(mod.Address, mod.ValueWritten);
    if (clrMDMod is null)
    {
        Console.WriteLine($"ERROR: did not find: " + mod.VerifyHeapOutput);
    }
    else
    {
        //Console.WriteLine($"{clrMDMod.VerifyObjectResult} {mod.VerifyHeapOutput}");
    }
}


///////////////////////
unsafe Modification? WriteAndVerifyClrMD(ulong location, byte[] newBuffer)
{
    byte[] old = new byte[newBuffer.Length];

    if (!spaces.ReadVirtual(location, old, out int read) || read != old.Length || newBuffer.SequenceEqual(old))
        return null;

    spaces.WriteVirtual(location, newBuffer, out int written);

    Modification? result = null;
    ClrSegment seg = runtime.Heap.GetSegmentByAddress(location) ?? throw new InvalidOperationException();
    foreach (ClrObject obj in seg.EnumerateObjects())
    {
        if (heap.IsObjectCorrupted(obj, out ObjectCorruption? corruption))
        {
            result = new()
            {
                Address = location,
                OriginalValue = old,
                ValueWritten = newBuffer,
                VerifyObjectResult = corruption
            };
            break;
        }
    }

    spaces.WriteVirtual(location, old, out written);

    return result;
}


unsafe Modification? WriteAndVerify<T>(ulong location, T newValue)
    where T : unmanaged
{
    byte[] old = new byte[sizeof(T)];
    byte[] newBuffer = new byte[sizeof(T)];

    Span<T> span = new(&newValue, 1);
    MemoryMarshal.Cast<T, byte>(span).CopyTo(newBuffer);

    if (!spaces.ReadVirtual(location, old, out int read) || read != old.Length || old.SequenceEqual(newBuffer))
        return null;

    spaces.WriteVirtual(location, newBuffer, out int written);

    Modification? result = null;

    using (DbgEngOutputHolder output = new(client, DEBUG_OUTPUT.ALL))
    {
        output.OutputReceived += (text, _) =>
        {
            if (!string.IsNullOrWhiteSpace(text) && !text.StartsWith("No heap corruption detected"))
            {

                result ??= new()
                {
                    Address = location,
                    OriginalValue = old,
                    ValueWritten = newBuffer,
                };

                result.VerifyHeapOutput += text;
            }
        };

        control.Execute(DEBUG_OUTCTL.THIS_CLIENT, "!verifyheap", DEBUG_EXECUTE.DEFAULT);
    }

    spaces.WriteVirtual(location, old, out written);

    return result;
}


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

void CorruptHeapAndTest(string output)
{
    List<Modification> failures = new();
    var sw = Stopwatch.StartNew();

    ulong last = 0;

    foreach (ClrSegment segment in runtime.Heap.Segments)
    {
        Console.Write($"Segment {segment.Address:x} Length: {segment.ObjectRange.Length:n0} bytes");
        var segWatch = Stopwatch.StartNew();
        for (ulong address = segment.ObjectRange.Start; address < segment.ObjectRange.End; address += (uint)dt.DataReader.PointerSize)
        {
            Add(WriteAndVerify(address, (byte)0xcc));
            Add(WriteAndVerify(address, (byte)0));

            Add(WriteAndVerify(address, (ushort)0xcccc));
            Add(WriteAndVerify(address, (ushort)0));

            Add(WriteAndVerify(address, (uint)0xcccccccc));
            Add(WriteAndVerify(address, (uint)0));

            if (dt.DataReader.PointerSize == 8)
            {
                Add(WriteAndVerify(address + 4, (byte)0xcc));
                Add(WriteAndVerify(address + 4, (byte)0));

                Add(WriteAndVerify(address + 4, (ushort)0xcccc));
                Add(WriteAndVerify(address + 4, (ushort)0));

                Add(WriteAndVerify(address + 4, (uint)0xcccccccc));
                Add(WriteAndVerify(address + 4, (uint)0));
            }

            if (sw.Elapsed.Seconds >= 30)
            {
                File.WriteAllText(output, JsonSerializer.Serialize(failures, new JsonSerializerOptions() { WriteIndented = true }));
                sw.Restart();
            }
        }

        Console.WriteLine($" completed in {segWatch.Elapsed}");
    }


    File.WriteAllText(args[0] + ".txt", JsonSerializer.Serialize(failures, new JsonSerializerOptions() { WriteIndented = true }));

    // End of Demo.

    void Add(Modification? modification)
    {
        if (modification is not null)
        {
            failures.Add(modification);
            last = modification.Address;
        }
    }
}

class Modification
{
    public ulong Address { get; init; }
    public byte[] OriginalValue { get; init; } = Array.Empty<byte>();
    public byte[] ValueWritten { get; init; } = Array.Empty<byte>();

    public string VerifyHeapOutput { get; set; } = "";
    public ObjectCorruption? VerifyObjectResult { get; set;}
}
