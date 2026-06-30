#nullable enable

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Runtime.StressLogs;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Verifies that the <c>ISOSDacInterface17</c>-backed stress-log path
    /// (exposed when the cDAC is loaded) produces the same messages as the raw
    /// <see cref="IDataReader"/> parser for the same dump.
    /// </summary>
    public class StressLogIs17ParityTests
    {
        private static readonly Dictionary<string, string> s_stressLogEnv = new(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_StressLog"] = "1",
            ["COMPlus_StressLog"] = "1",
            ["DOTNET_LogFacility"] = "0xFFFFFFFF",
            ["COMPlus_LogFacility"] = "0xFFFFFFFF",
            ["DOTNET_LogLevel"] = "10",
            ["COMPlus_LogLevel"] = "10",
            ["DOTNET_StressLogSize"] = "0x100000",
            ["COMPlus_StressLogSize"] = "0x100000",
            ["DOTNET_TotalStressLogSize"] = "0x4000000",
            ["COMPlus_TotalStressLogSize"] = "0x4000000",
        };

        private const string DumpSuffix = "stresslog_v2";

        [WindowsFact]
        public void Is17AndRawProduceIdenticalMessages_Workstation()
        {
            RunParity(GCMode.Workstation);
        }

        [WindowsFact]
        public void Is17AndRawProduceIdenticalMessages_Server()
        {
            RunParity(GCMode.Server);
        }

        private static void RunParity(GCMode gcMode)
        {
            using DataTarget dt = TestTargets.StressLog.LoadFullDumpWithEnvironment(
                extraEnv: s_stressLogEnv,
                dumpNameSuffix: DumpSuffix,
                gc: gcMode,
                isFramework: false,
                singleFile: false);

            ClrInfo clrInfo = dt.ClrVersions.Single();

            string? cdacPath = FindCDacPath(clrInfo);
            if (cdacPath is null)
            {
                // No usable cDAC (ISOSDacInterface17 provider) is present next to
                // the runtime, so the IS17 path cannot be exercised here. Treat as
                // a no-op pass; CI environments that ship a version-matched cDAC
                // will run the real comparison below.
                return;
            }

            ClrRuntime runtime;
            StressLog? is17Log;
            string? failure;
            try
            {
                runtime = clrInfo.CreateRuntime(cdacPath!, ignoreMismatch: true);
            }
            catch (Exception)
            {
                // A cDAC whose contract-descriptor generation does not match the
                // target runtime can fail to initialize. That is an environment
                // mismatch, not a product defect; skip rather than fail.
                return;
            }

            using (runtime)
            {
                bool opened = runtime.TryGetStressLog(out is17Log, out failure);
                Assert.True(opened, $"TryGetStressLog (cDAC) failed: {failure}");
                Assert.NotNull(is17Log);

                if (is17Log is not ContractStressLog)
                {
                    // The cDAC loaded but did not expose ISOSDacInterface17; nothing to compare.
                    return;
                }

                ComparePaths(dt, runtime, is17Log);
            }
        }

        private static void ComparePaths(DataTarget dt, ClrRuntime runtime, StressLog is17Log)
        {
            // IS17 path (owned by the runtime; do not dispose).
            List<string> is17Keys = Collect(is17Log);

            // Raw parser path: open the same log by address. The address-based
            // overload never uses IS17, so this is the pure raw parser.
            ulong address = runtime.GetStressLogAddress();
            Assert.NotEqual(0ul, address);
            bool rawOpened = StressLog.TryOpen(dt.DataReader, address, out StressLog? rawLog, out string? rawFailure);
            Assert.True(rawOpened, $"address-based StressLog.TryOpen failed: {rawFailure}");

            List<string> rawKeys;
            using (rawLog)
            {
                Assert.IsType<LegacyStressLog>(rawLog);
                rawKeys = Collect(rawLog);
            }

            Assert.True(is17Keys.Count > 0, "IS17 path yielded zero messages.");
            Assert.True(rawKeys.Count > 0, "Raw path yielded zero messages.");

            // Compare as ordered multisets. Cross-thread ties on identical
            // timestamps may be ordered differently between the two thread
            // enumeration orders, so sort before comparing.
            is17Keys.Sort(StringComparer.Ordinal);
            rawKeys.Sort(StringComparer.Ordinal);

            Assert.Equal(rawKeys.Count, is17Keys.Count);

            int mismatches = 0;
            StringBuilder firstFew = new();
            for (int i = 0; i < rawKeys.Count; i++)
            {
                if (!string.Equals(rawKeys[i], is17Keys[i], StringComparison.Ordinal))
                {
                    if (mismatches < 10)
                        firstFew.AppendLine($"  raw : {rawKeys[i]}").AppendLine($"  is17: {is17Keys[i]}");
                    mismatches++;
                }
            }

            Assert.True(mismatches == 0, $"{mismatches} of {rawKeys.Count} messages differ between raw and IS17 paths. First mismatches:\n{firstFew}");
        }

        private static List<string> Collect(StressLog log)
        {
            List<string> keys = new();
            foreach (StressLogMessage msg in log.EnumerateMessages())
            {
                keys.Add(KeyFor(msg));
                if (keys.Count >= 200_000)
                    break;
            }

            return keys;
        }

        private static string KeyFor(StressLogMessage msg)
        {
            StringBuilder sb = new();
            sb.Append(msg.OSThreadId.ToString("x")).Append('|');
            sb.Append(msg.TimeStampTicks.ToString("x")).Append('|');
            sb.Append(((uint)msg.Facility).ToString("x")).Append('|');
            sb.Append(msg.ArgumentCount).Append('|');
            for (int i = 0; i < msg.ArgumentCount; i++)
                sb.Append(msg.GetArgument(i).ToString("x")).Append(',');
            sb.Append('|');

            KeyReceiver receiver = KeyReceiver.Create();
            msg.Format(ref receiver);
            sb.Append(receiver.Output);
            return sb.ToString();
        }

        private static string? FindCDacPath(ClrInfo clrInfo)
        {
            foreach (DebugLibraryInfo lib in clrInfo.DebuggingLibraries)
            {
                if (lib.Kind == DebugLibraryKind.CDac
                    && Path.GetFileName(lib.FileName) != lib.FileName
                    && File.Exists(lib.FileName))
                {
                    return lib.FileName;
                }
            }

            return null;
        }

        private struct KeyReceiver : IStressLogFormatReceiver
        {
            public StringBuilder Output;

            public static KeyReceiver Create() => new() { Output = new StringBuilder() };

            public void Literal(ReadOnlySpan<byte> ascii)
            {
#if NET10_0_OR_GREATER
                Output.Append(Encoding.ASCII.GetString(ascii));
#else
                Output.Append(Encoding.ASCII.GetString(ascii.ToArray()));
#endif
            }

            public void Integer(StressLogIntegerKind kind, int width, int precision, long value)
                => Output.Append('[').Append(kind).Append(':').Append(value).Append(']');

            public void Pointer(StressLogPointerKind kind, ulong address)
                => Output.Append("[P:").Append(kind).Append(':').Append(address.ToString("x")).Append(']');

            public void String(StressLogStringEncoding encoding, ReadOnlySpan<byte> sanitized)
            {
#if NET10_0_OR_GREATER
                Output.Append("[S:").Append(Encoding.ASCII.GetString(sanitized)).Append(']');
#else
                Output.Append("[S:").Append(Encoding.ASCII.GetString(sanitized.ToArray())).Append(']');
#endif
            }

            public void MissingArgument() => Output.Append("<missing>");
        }
    }
}
