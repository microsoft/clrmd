// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime.StressLogs;
using Microsoft.Diagnostics.Runtime.StressLogs.Internal;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Unit tests for the stress log reader. The tests cover the parser,
    /// sanitizer, module table, allocation budget, and end-to-end open/iterate
    /// path with deliberately malformed inputs to confirm the reader stays
    /// bounded and never produces unsanitized output.
    /// </summary>
    public class StressLogTests
    {
        private const ulong FormatAddrSimple = 0x10000;
        private const ulong FormatAddrPercentN = 0x10100;
        private const ulong FormatAddrPositional = 0x10200;
        private const ulong FormatAddrStarWidth = 0x10300;
        private const ulong FormatAddrLong = 0x10400;
        private const ulong StringArgAddr = 0x20000;
        private const ulong StringArgAnsi = 0x20100;
        private const ulong StringArgUtf16 = 0x20200;

        // ---- FormatStringSanitizer ---------------------------------------

        [Fact]
        public void Sanitizer_PreservesPrintableAscii()
        {
            ReadOnlySpan<byte> input = Encoding.ASCII.GetBytes("Hello %d World").AsSpan();
            byte[] dst = new byte[input.Length];
            int written = StressLogSanitizer.SanitizeAscii(input, dst);
            Assert.Equal(input.Length, written);
            Assert.Equal(Encoding.ASCII.GetString(input.ToArray()), Encoding.ASCII.GetString(dst));
        }

        [Fact]
        public void Sanitizer_ReplacesControlAndAnsiBytes()
        {
            // ESC [ 3 1 m   "Hi"   \x07 (BEL)   \x00
            byte[] input = { 0x1B, (byte)'[', (byte)'3', (byte)'1', (byte)'m', (byte)'H', (byte)'i', 0x07 };
            byte[] dst = new byte[input.Length];
            int written = StressLogSanitizer.SanitizeAscii(input, dst);
            Assert.Equal(input.Length, written);
            Assert.Equal((byte)'.', dst[0]); // ESC
            Assert.Equal((byte)'[', dst[1]); // printable
            Assert.Equal((byte)'H', dst[5]);
            Assert.Equal((byte)'i', dst[6]);
            Assert.Equal((byte)'.', dst[7]); // BEL
        }

        [Fact]
        public void Sanitizer_Utf16TerminatesAtNull()
        {
            // 'A' 0x00 'B' 0x00 0x00 0x00 'C' 0x00
            byte[] input = { 0x41, 0, 0x42, 0, 0, 0, 0x43, 0 };
            byte[] dst = new byte[8];
            int written = StressLogSanitizer.SanitizeUtf16(input, dst);
            Assert.Equal(2, written); // stops at the third pair which is NUL
            Assert.Equal((byte)'A', dst[0]);
            Assert.Equal((byte)'B', dst[1]);
        }

        // ---- FormatStringParser ------------------------------------------

        private struct CaptureReceiver : IStressLogFormatReceiver
        {
            public StringBuilder Output;
            public int IntegerCalls;
            public int PointerCalls;
            public int StringCalls;
            public int LiteralCalls;
            public int MissingCalls;

            public static CaptureReceiver Create() => new() { Output = new StringBuilder() };

            public void Literal(ReadOnlySpan<byte> ascii)
            {
                LiteralCalls++;
#if NET10_0_OR_GREATER
                Output.Append(Encoding.ASCII.GetString(ascii));
#else
                Output.Append(Encoding.ASCII.GetString(ascii.ToArray()));
#endif
            }
            public void Integer(StressLogIntegerKind kind, int width, int precision, long value)
            {
                IntegerCalls++;
                Output.Append('[').Append(kind).Append(':').Append(value).Append(']');
            }
            public void Pointer(StressLogPointerKind kind, ulong address)
            {
                PointerCalls++;
                Output.Append("[P:").Append(kind).Append(':').Append(address.ToString("x")).Append(']');
            }
            public void String(StressLogStringEncoding encoding, ReadOnlySpan<byte> sanitized)
            {
                StringCalls++;
#if NET10_0_OR_GREATER
                Output.Append("[S:").Append(Encoding.ASCII.GetString(sanitized)).Append(']');
#else
                Output.Append("[S:").Append(Encoding.ASCII.GetString(sanitized.ToArray())).Append(']');
#endif
            }
            public void MissingArgument()
            {
                MissingCalls++;
                Output.Append("<missing>");
            }
        }

        private static void Parse(string format, ulong[] args, out CaptureReceiver receiver,
                                  Dictionary<ulong, byte[]>? memory = null)
        {
            receiver = CaptureReceiver.Create();
            ArgumentResolver resolver = new ArgumentResolver(new SyntheticDataReader(memory ?? new()));
            FormatStringParser.Parse(Encoding.ASCII.GetBytes(format), args, resolver, ref receiver);
        }

        [Fact]
        public void Parser_BasicIntegerHexPointer()
        {
            Parse("%d %x %p", new ulong[] { 42, 0xDEAD, 0xCAFEBABE }, out CaptureReceiver r);
            Assert.Equal(2, r.IntegerCalls);
            Assert.Equal(1, r.PointerCalls);
            Assert.Equal(0, r.MissingCalls);
        }

        [Fact]
        public void Parser_DoublePercentEmitsLiteral()
        {
            Parse("100%% done", Array.Empty<ulong>(), out CaptureReceiver r);
            string output = r.Output.ToString();
            Assert.Equal("100% done", output);
            Assert.Equal(0, r.IntegerCalls);
        }

        [Fact]
        public void Parser_RejectsPercentN()
        {
            Parse("before %n after", new ulong[] { 0x1234 }, out CaptureReceiver r);
            // %n must not be invoked as Pointer or Integer; the rejected
            // specifier is rendered as a literal.
            Assert.Equal(0, r.PointerCalls);
            Assert.Equal(0, r.IntegerCalls);
            Assert.Contains("%n", r.Output.ToString());
        }

        [Fact]
        public void Parser_RejectsPositionalSpecifier()
        {
            Parse("%1$s and %2$d", new ulong[] { StringArgAddr, 99 }, out CaptureReceiver r);
            Assert.Equal(0, r.StringCalls);
            Assert.Equal(0, r.IntegerCalls);
        }

        [Fact]
        public void Parser_RejectsIndirectWidth()
        {
            Parse("%*d", new ulong[] { 5, 42 }, out CaptureReceiver r);
            Assert.Equal(0, r.IntegerCalls);
        }

        [Fact]
        public void Parser_MissingArgumentEmitsToken()
        {
            Parse("%d %d %d", new ulong[] { 1 }, out CaptureReceiver r);
            Assert.Equal(1, r.IntegerCalls);
            Assert.Equal(2, r.MissingCalls);
        }

        [Fact]
        public void Parser_ClampsHugeWidthAndPrecision()
        {
            // Width/precision are clamped before being surfaced. The
            // important thing is that the parser does not allocate or read
            // 2 GB of memory; we just verify that exactly one integer was
            // emitted.
            Parse("%2147483647.2147483647d", new ulong[] { 7 }, out CaptureReceiver r);
            Assert.Equal(1, r.IntegerCalls);
        }

        [Fact]
        public void Parser_LengthModifiersAreStripped()
        {
            Parse("%lu %hd %lld %I64x %zd", new ulong[] { 1, 2, 3, 4, 5 }, out CaptureReceiver r);
            Assert.Equal(5, r.IntegerCalls);
        }

        [Fact]
        public void Parser_TypedPointerSpecifiers()
        {
            Parse("%pM %pT %pV %pK %p", new ulong[] { 1, 2, 3, 4, 5 }, out CaptureReceiver r);
            Assert.Equal(5, r.PointerCalls);
        }

        [Fact]
        public void Parser_StringReadsAndSanitizes()
        {
            // Backing memory carries control bytes and ANSI escapes; the
            // parser must hand the receiver only sanitized ASCII.
            byte[] withControlBytes = { 0x1B, (byte)'[', (byte)'3', (byte)'1', (byte)'m', (byte)'!', 0 };
            Dictionary<ulong, byte[]> mem = new() { { StringArgAnsi, withControlBytes } };
            Parse("hello %s end", new ulong[] { StringArgAnsi }, out CaptureReceiver r, mem);
            Assert.Equal(1, r.StringCalls);
            string output = r.Output.ToString();
            // Every byte must be printable 7-bit ASCII. The ESC byte must
            // have been replaced before reaching the receiver, and tab/
            // newline bytes from anywhere in the input must also be replaced.
            foreach (char c in output)
                Assert.True(c >= 0x20 && c <= 0x7E, $"Output contains non-printable byte 0x{(int)c:X} ('{output}')");
            Assert.Contains(".[31m!", output);
        }

        // ---- StressLogModuleTable ----------------------------------------

        [Fact]
        public void ModuleTable_FallsBackWhenSizeOutOfRange()
        {
            // First module's size is bigger than maxOffset → table rejected.
            byte[] entries = new byte[StressLogConstants.MaxModules * 16];
            BitConverter.GetBytes((ulong)0x100).CopyTo(entries, 0);                    // baseAddress
            BitConverter.GetBytes(StressLogConstants.FormatOffsetMax + 1).CopyTo(entries, 8); // size, too big

            StressLogModuleTable table = StressLogModuleTable.BuildInProcess(
                StressLogLayout.CoreX64,
                entries,
                legacyModuleOffset: 0x100,
                hasModuleTable: true,
                maxOffset: StressLogConstants.FormatOffsetMax);

            // Falls back to single-module mode (count == 0).
            Assert.Equal(0, table.Count);
        }

        [Fact]
        public void ModuleTable_ResolvesOffsetWhenValid()
        {
            byte[] entries = new byte[StressLogConstants.MaxModules * 16];
            ulong baseAddr = 0x7FF000;
            ulong size = 4UL * 1024 * 1024;
            BitConverter.GetBytes(baseAddr).CopyTo(entries, 0);
            BitConverter.GetBytes(size).CopyTo(entries, 8);

            StressLogModuleTable table = StressLogModuleTable.BuildInProcess(
                StressLogLayout.CoreX64,
                entries,
                legacyModuleOffset: baseAddr,
                hasModuleTable: true,
                maxOffset: StressLogConstants.FormatOffsetMax);

            Assert.Equal(1, table.Count);
            Assert.True(table.TryResolveFormatOffset(0x123, out ulong resolved));
            Assert.Equal(baseAddr + 0x123, resolved);
        }

        // ---- AllocationBudget --------------------------------------------

        [Fact]
        public void AllocationBudget_RefusesBeyondCap()
        {
            AllocationBudget budget = new(1024);
            Assert.True(budget.TryReserve(512));
            Assert.True(budget.TryReserve(512));
            Assert.False(budget.TryReserve(1));
            budget.Release(512);
            Assert.True(budget.TryReserve(512));
        }

        // ---- End-to-end with a synthetic stress log ----------------------

        [Fact]
        public void EndToEnd_TruncatedHeaderFailsTryOpen()
        {
            // Reader returns 0 bytes for any address.
            SyntheticDataReader reader = new(new());
            Assert.False(StressLog.TryOpen(reader, 0x1000, out StressLog? log));
            Assert.Null(log);
        }

        [Fact]
        public void EndToEnd_NullAddressFailsTryOpen()
        {
            SyntheticDataReader reader = new(new());
            Assert.False(StressLog.TryOpen(reader, 0, out StressLog? log));
            Assert.Null(log);
        }

        [Fact]
        public void EndToEnd_SingleMessageRoundTrip()
        {
            SyntheticStressLogBuilder builder = new SyntheticStressLogBuilder();
            ulong stressLogAddr = builder.Build(out _, out _);
            SyntheticDataReader reader = new(builder.Memory);

            Assert.True(StressLog.TryOpen(reader, stressLogAddr, out StressLog? log));
            Assert.NotNull(log);

            int count = 0;
            int diagnosticCount = 0;
            log!.Diagnostic += _ => diagnosticCount++;
            foreach (StressLogMessage msg in log.EnumerateMessages())
            {
                count++;
                Assert.Equal(SyntheticStressLogBuilder.ThreadId, msg.OSThreadId);
                Assert.Equal((StressLogFacility)SyntheticStressLogBuilder.Facility, msg.Facility);
                Assert.Equal(0, msg.ArgumentCount);
                if (count > 100) break;
            }

            Assert.Equal(1, count);
            log.Dispose();
        }

        [Fact]
        public void EndToEnd_ConcurrentEnumerationsDoNotInterfere()
        {
            // Two threads enumerating the same StressLog instance must each
            // see exactly the messages they were yielded; the per-enumeration
            // context isolates argument scratch / current iterator from the
            // shared instance.
            SyntheticStressLogBuilder builder = new SyntheticStressLogBuilder();
            ulong stressLogAddr = builder.Build(out _, out _);
            SyntheticDataReader reader = new(builder.Memory);

            Assert.True(StressLog.TryOpen(reader, stressLogAddr, out StressLog? log));
            Assert.NotNull(log);

            using (log)
            {
                int countA = 0;
                int countB = 0;
                Exception? errorA = null;
                Exception? errorB = null;

                System.Threading.Thread tA = new(() =>
                {
                    try
                    {
                        for (int loop = 0; loop < 50; loop++)
                            foreach (StressLogMessage msg in log!.EnumerateMessages())
                            {
                                Assert.Equal(SyntheticStressLogBuilder.ThreadId, msg.OSThreadId);
                                countA++;
                            }
                    }
                    catch (Exception ex) { errorA = ex; }
                });
                System.Threading.Thread tB = new(() =>
                {
                    try
                    {
                        for (int loop = 0; loop < 50; loop++)
                            foreach (StressLogMessage msg in log!.EnumerateMessages())
                            {
                                Assert.Equal(SyntheticStressLogBuilder.ThreadId, msg.OSThreadId);
                                countB++;
                            }
                    }
                    catch (Exception ex) { errorB = ex; }
                });

                tA.Start();
                tB.Start();
                tA.Join();
                tB.Join();

                Assert.Null(errorA);
                Assert.Null(errorB);
                Assert.Equal(50, countA);
                Assert.Equal(50, countB);
            }
        }
    }
}
