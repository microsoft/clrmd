#nullable enable

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.StressLogs;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class StressLogIntegrationTests
    {
        // The runtime's stress log is enabled by environment variable. We set both
        // the DOTNET_ and COMPlus_ prefixes so this works for .NET 5+ (DOTNET_) and
        // older runtimes / .NET Framework (COMPlus_).
        //
        // LogFacility=0xFFFFFFFF turns on every subsystem; the per-thread buffer
        // is sized large enough (1 MB) that a couple of forced compacting
        // collections won't be wrapped out by other chatter.
        //
        // LogLevel=10 == LL_INFO100000, which is the most permissive level the
        // GC writers check against.
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

        // Suffix bumps when the environment changes so old cached dumps don't
        // satisfy a new test that needs different GC behavior.
        private const string DumpSuffix = "stresslog_v2";

        [Fact]
        public void CanOpenStressLog_Core_Workstation()
        {
            RunAssertions(GCMode.Workstation, isFramework: false, singleFile: false, requireGcStartEnd: true);
        }

        [Fact]
        public void CanOpenStressLog_Core_Server()
        {
            RunAssertions(GCMode.Server, isFramework: false, singleFile: false, requireGcStartEnd: true);
        }

        [WindowsFact]
        public void CanOpenStressLog_Core_SingleFile()
        {
            // Self-contained single-file publishes use the same Core format
            // strings as a framework-dependent publish; the stress log content
            // is identical and we can assert the same set of known formats.
            RunAssertions(GCMode.Workstation, isFramework: false, singleFile: true, requireGcStartEnd: true);
        }

        [FrameworkFact]
        public void CanOpenStressLog_Framework_Workstation()
        {
            // .NET Framework's gcmsg.inl shares the GcStart/GcEnd/GcRoot
            // format strings with Core, so they are recognized by the parser.
            RunAssertions(GCMode.Workstation, isFramework: true, singleFile: false, requireGcStartEnd: true);
        }

        [FrameworkFact]
        public void CanOpenStressLog_Framework_Server()
        {
            RunAssertions(GCMode.Server, isFramework: true, singleFile: false, requireGcStartEnd: true);
        }

        private static void RunAssertions(GCMode gcMode, bool isFramework, bool singleFile, bool requireGcStartEnd)
        {
            using DataTarget dt = TestTargets.StressLog.LoadFullDumpWithEnvironment(
                extraEnv: s_stressLogEnv,
                dumpNameSuffix: DumpSuffix,
                gc: gcMode,
                isFramework: isFramework,
                singleFile: singleFile);

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            bool opened = runtime.TryGetStressLog(out StressLog? stressLog, out string? failureReason);
            Assert.True(opened, BuildDiagnosticMessage($"runtime.TryGetStressLog returned false: {failureReason}", runtime, stressLog: null));
            Assert.NotNull(stressLog);

            // The stress log is owned by the runtime; do not dispose it here.
            {
                List<StressLogMessage> messages = new();
                Dictionary<StressLogKnownFormat, int> knownCounts = new();
                List<StressLogDiagnostic> diagnostics = new();

                stressLog!.Diagnostic += d =>
                {
                    lock (diagnostics)
                    {
                        if (diagnostics.Count < 32)
                            diagnostics.Add(d);
                    }
                };

                foreach (StressLogMessage msg in stressLog.EnumerateMessages())
                {
                    messages.Add(msg);
                    if (msg.KnownFormat != StressLogKnownFormat.None)
                    {
                        knownCounts.TryGetValue(msg.KnownFormat, out int n);
                        knownCounts[msg.KnownFormat] = n + 1;
                    }

                    // Cap the enumeration to keep the test bounded even if the
                    // dump is unexpectedly large; we only need a representative
                    // sample for the assertions below.
                    if (messages.Count >= 200_000)
                        break;
                }

                Assert.True(
                    messages.Count > 0,
                    BuildDiagnosticMessage("Stress log was opened but yielded zero messages", runtime, stressLog, messages, knownCounts, diagnostics));

                // Per-format arg-count sanity. The format strings come from the
                // runtime's gcmsg.inl and have stable arities; if a format is
                // observed it must have the documented argument count.
                AssertArgCount(messages, StressLogKnownFormat.GcStart, expected: 3);
                AssertArgCount(messages, StressLogKnownFormat.GcEnd, expected: 3);
                AssertArgCount(messages, StressLogKnownFormat.GcRoot, expected: 4);
                AssertArgCount(messages, StressLogKnownFormat.GcRootPromote, expected: 3);
                AssertArgCount(messages, StressLogKnownFormat.GcPlugMove, expected: 3);

                if (requireGcStartEnd)
                {
                    // The test target forces two compacting GCs, so GcStart/GcEnd
                    // are expected to appear. Other known formats are best-effort:
                    // they depend on whether the GC actually had relocations to
                    // emit, which can vary by runtime version and GC mode.
                    Assert.True(
                        knownCounts.GetValueOrDefault(StressLogKnownFormat.GcStart, 0) > 0,
                        BuildDiagnosticMessage("GcStart not observed", runtime, stressLog, messages, knownCounts, diagnostics));
                    Assert.True(
                        knownCounts.GetValueOrDefault(StressLogKnownFormat.GcEnd, 0) > 0,
                        BuildDiagnosticMessage("GcEnd not observed", runtime, stressLog, messages, knownCounts, diagnostics));
                }
            }
        }

        private static void AssertArgCount(IEnumerable<StressLogMessage> messages, StressLogKnownFormat format, int expected)
        {
            foreach (StressLogMessage msg in messages)
            {
                if (msg.KnownFormat == format)
                {
                    Assert.True(
                        msg.ArgumentCount == expected,
                        $"{format} message had ArgumentCount={msg.ArgumentCount}, expected {expected}.");
                }
            }
        }

        private static string BuildDiagnosticMessage(
            string headline,
            ClrRuntime runtime,
            StressLog? stressLog,
            IReadOnlyList<StressLogMessage>? messages = null,
            IReadOnlyDictionary<StressLogKnownFormat, int>? knownCounts = null,
            IReadOnlyList<StressLogDiagnostic>? diagnostics = null)
        {
            System.Text.StringBuilder sb = new();
            sb.AppendLine(headline);
            sb.AppendLine($"  OS: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"  Architecture: {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($"  ClrVersion: {runtime.ClrInfo.Version} flavor={runtime.ClrInfo.Flavor}");
            sb.AppendLine($"  StressLog opened: {stressLog is not null}");
            if (messages is not null)
            {
                sb.AppendLine($"  Total messages parsed: {messages.Count}");
                int unknown = 0;
                foreach (StressLogMessage m in messages)
                    if (m.KnownFormat == StressLogKnownFormat.None) unknown++;
                sb.AppendLine($"  Unknown-format messages: {unknown}");
            }

            if (knownCounts is not null)
            {
                foreach (KeyValuePair<StressLogKnownFormat, int> kvp in knownCounts)
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            if (diagnostics is not null && diagnostics.Count > 0)
            {
                sb.AppendLine($"  Diagnostics ({diagnostics.Count}):");
                foreach (StressLogDiagnostic d in diagnostics)
                    sb.AppendLine($"    {d}");
            }

            return sb.ToString();
        }
    }
}
