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
        public void Sanitizer_PreservesWhitespaceControlBytes()
        {
            // \t \n \r are legitimate (format strings commonly end in \n);
            // \v (0x0B) and \f (0x0C) are not whitespace we need to preserve.
            byte[] input = { (byte)'\t', (byte)'\n', (byte)'\r', 0x0B, 0x0C };
            byte[] dst = new byte[input.Length];
            int written = StressLogSanitizer.SanitizeAscii(input, dst);
            Assert.Equal(input.Length, written);
            Assert.Equal((byte)'\t', dst[0]);
            Assert.Equal((byte)'\n', dst[1]);
            Assert.Equal((byte)'\r', dst[2]);
            Assert.Equal((byte)'.', dst[3]);
            Assert.Equal((byte)'.', dst[4]);
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
                                  Dictionary<ulong, byte[]>? memory = null,
                                  int pointerSize = 8)
        {
            receiver = CaptureReceiver.Create();
            SyntheticDataReader reader = pointerSize == 4
                ? new SyntheticDataReader(memory ?? new(), pointerSize: 4, architecture: System.Runtime.InteropServices.Architecture.X86)
                : new SyntheticDataReader(memory ?? new());
            ArgumentResolver resolver = new ArgumentResolver(reader, maxStringBytes: 256, pointerSize: pointerSize);
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

        // ---- 32-bit printf semantics: sign extension and 64-bit length modifier ----

        [Fact]
        public void Parser_X86_DecimalSpecSignExtendsInt()
        {
            // The runtime stores `int -1` as the 32-bit value 0xFFFFFFFF on a
            // 32-bit target; when read into a ulong slot it is zero-extended
            // to 0x00000000FFFFFFFF. %d must reinterpret the low 32 bits as a
            // signed int and emit -1, not 4294967295.
            Parse("%d", new ulong[] { 0xFFFFFFFFUL }, out CaptureReceiver r, pointerSize: 4);
            Assert.Equal(1, r.IntegerCalls);
            Assert.Contains("[Decimal:-1]", r.Output.ToString());
        }

        [Fact]
        public void Parser_X86_UnsignedSpecZeroExtendsUint()
        {
            // %u of 0xFFFFFFFF on x86 is 4294967295 (unsigned int max), not -1.
            Parse("%u", new ulong[] { 0xFFFFFFFFUL }, out CaptureReceiver r, pointerSize: 4);
            Assert.Equal(1, r.IntegerCalls);
            Assert.Contains("[Unsigned:4294967295]", r.Output.ToString());
        }

        [Fact]
        public void Parser_X86_I64UnsignedConsumesTwoSlots()
        {
            // On 32-bit, fprintf's %I64u/%llu takes 8 bytes of arg storage,
            // i.e. two consecutive size_t-wide slots: low dword first.
            // 0x00000000_FFFFFFFF (low=0xFFFFFFFF, high=0) -> 4294967295.
            Parse("%I64u", new ulong[] { 0xFFFFFFFFUL, 0UL }, out CaptureReceiver r, pointerSize: 4);
            Assert.Equal(1, r.IntegerCalls);
            Assert.Contains("[Unsigned:4294967295]", r.Output.ToString());
        }

        [Fact]
        public void Parser_X86_I64UnsignedHighBitsCombined()
        {
            // 0x00000001_00000000 = low=0, high=1 -> 4294967296.
            Parse("%I64u", new ulong[] { 0UL, 1UL }, out CaptureReceiver r, pointerSize: 4);
            Assert.Equal(1, r.IntegerCalls);
            Assert.Contains("[Unsigned:4294967296]", r.Output.ToString());
        }

        [Fact]
        public void Parser_X86_LongLongHexConsumesTwoSlots()
        {
            // %llx on 32-bit: low=0xCAFEBABE, high=0xDEADBEEF -> 0xDEADBEEFCAFEBABE.
            Parse("%llx", new ulong[] { 0xCAFEBABEUL, 0xDEADBEEFUL }, out CaptureReceiver r, pointerSize: 4);
            Assert.Equal(1, r.IntegerCalls);
            // CaptureReceiver formats Integer values as decimal, so just check call count and value.
            Assert.Contains("[Hex:" + unchecked((long)0xDEADBEEFCAFEBABEUL) + "]", r.Output.ToString());
        }

        [Fact]
        public void Parser_X86_I64UnsignedRunsOutOfArgs()
        {
            // Only one slot supplied for an %I64u that wants two. Must not
            // throw, must not read out of bounds: parser sees the low slot
            // as the value (high defaulted to 0) and emits the integer
            // without crashing.
            Parse("%I64u", new ulong[] { 0xFFFFFFFFUL }, out CaptureReceiver r, pointerSize: 4);
            // Either Integer with low value, or MissingArgument -- both are
            // acceptable so long as the parser stays bounded.
            Assert.True(r.IntegerCalls + r.MissingCalls >= 1);
        }

        [Fact]
        public void Parser_X64_DecimalSpecRoundTripsNegative()
        {
            // On 64-bit, `int -1` is sign-extended at store time to
            // 0xFFFFFFFFFFFFFFFF; %d must still emit -1.
            Parse("%d", new ulong[] { 0xFFFFFFFFFFFFFFFFUL }, out CaptureReceiver r, pointerSize: 8);
            Assert.Equal(1, r.IntegerCalls);
            Assert.Contains("[Decimal:-1]", r.Output.ToString());
        }

        // ---- Fuzz: random format strings + random args must not crash ----

        [Theory]
        [InlineData(0xC0FFEE, 8)]
        [InlineData(unchecked((int)0xDEADBEEF), 8)]
        [InlineData(0x12345678, 8)]
        [InlineData(0xC0FFEE, 4)]
        [InlineData(unchecked((int)0xDEADBEEF), 4)]
        [InlineData(0x12345678, 4)]
        public void Parser_FuzzNeverThrowsAndStaysBounded(int seed, int pointerSize)
        {
            // Feed random bytes (including 0x25 '%' to ensure many specifier
            // attempts) into the parser. The parser must always terminate
            // and never throw an unhandled exception. This exercises every
            // combination of length modifier, width/precision overflow,
            // truncated specifier at end-of-string, etc., for both pointer
            // sizes so the x86 64-bit-arg-combining path is covered.
            Random rng = new(seed);
            for (int trial = 0; trial < 250; trial++)
            {
                int len = rng.Next(1, 256);
                byte[] format = new byte[len];
                for (int i = 0; i < len; i++)
                {
                    int r = rng.Next(0, 100);
                    if (r < 25)
                        format[i] = (byte)'%';
                    else if (r < 35)
                        format[i] = (byte)"diuxXpsScnhlLjztI*0123456789. -+#"[rng.Next(33)];
                    else
                        format[i] = (byte)rng.Next(0x20, 0x7F); // printable ASCII
                }

                int argCount = rng.Next(0, 16);
                ulong[] args = new ulong[argCount];
                for (int i = 0; i < argCount; i++)
                    args[i] = (ulong)rng.NextInt64();

                CaptureReceiver receiver = CaptureReceiver.Create();
                SyntheticDataReader reader = pointerSize == 4
                    ? new SyntheticDataReader(new(), pointerSize: 4, architecture: System.Runtime.InteropServices.Architecture.X86)
                    : new SyntheticDataReader(new());
                ArgumentResolver resolver = new ArgumentResolver(reader, maxStringBytes: 256, pointerSize: pointerSize);

                // Must not throw.
                FormatStringParser.Parse<CaptureReceiver>(format, args, resolver, ref receiver);

                // Must not advance past argCount + a small bound (per-spec
                // 64-bit consumes 2; rough upper bound is 2 * format.Length).
                Assert.True(receiver.LiteralCalls + receiver.IntegerCalls + receiver.PointerCalls
                            + receiver.StringCalls + receiver.MissingCalls <= 2 * format.Length + 8,
                    "Parser emitted unbounded tokens for input.");
            }
        }

        [Fact]
        public void Parser_FuzzPercentNeverInterpreted()
        {
            // Stress the parser with format strings full of '%' and length
            // modifiers but no valid conversion specifier. Verifies that
            // %n, positional %1$, etc. never get treated as a real specifier.
            string[] danger = {
                "%n", "%hn", "%ln", "%lln", "%hhn",
                "%1$d", "%99$s",
                "%*d", "%.*d", "%-1d",
                "%2147483648d", "%.2147483648s",
                "%llz", "%I63d", "%qd",
                "%pZ", "%pX", "%phM",
            };
            foreach (string f in danger)
            {
                Parse(f, new ulong[] { 1, 2, 3, 4 }, out CaptureReceiver r);
                // %n family must never be interpreted (no Pointer, no Integer
                // for those specs; for the others we simply require the
                // parser to terminate cleanly and not throw).
                if (f.Contains("%n", System.StringComparison.Ordinal) || f.Contains("$", System.StringComparison.Ordinal))
                {
                    Assert.Equal(0, r.PointerCalls);
                }
            }
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

        // ---- 32-bit (Win-x86) ---------------------------------------------

        [Fact]
        public void Layout_x86_RoundTripSingleMessage()
        {
            SyntheticStressLogBuilder builder = new();
            ulong addr = builder.Build(StressLogLayout.CoreWinX86, out _, out _);
            SyntheticDataReader reader = new(builder.Memory, pointerSize: 4, architecture: System.Runtime.InteropServices.Architecture.X86);

            Assert.True(StressLog.TryOpen(reader, addr, out StressLog? log));
            Assert.NotNull(log);
            Assert.Equal(4, log!.PointerSize);

            int count = 0;
            foreach (StressLogMessage msg in log.EnumerateMessages())
            {
                count++;
                Assert.Equal(SyntheticStressLogBuilder.ThreadId, msg.OSThreadId);
                Assert.Equal((StressLogFacility)SyntheticStressLogBuilder.Facility, msg.Facility);
                Assert.Equal(0, msg.ArgumentCount);
                if (count > 10) break;
            }

            Assert.Equal(1, count);
            log.Dispose();
        }

        [Fact]
        public void Layout_x86_HighBitPointersZeroExtend()
        {
            // Place the StressLog header at a high address (>= 0x80000000).
            // If any pointer-sized read inadvertently sign-extended, the
            // resulting ulong would be 0xFFFFFFFF80000000... and the chunk
            // walk would fail. Successful enumeration confirms zero-extension.
            HighBitBuilder builder = new(stressLogAddr: 0x80100000, threadAddr: 0x80200000, chunkAddr: 0x80300000);
            ulong addr = builder.Build(StressLogLayout.CoreWinX86, out _, out _);
            SyntheticDataReader reader = new(builder.Memory, pointerSize: 4, architecture: System.Runtime.InteropServices.Architecture.X86);

            Assert.True(StressLog.TryOpen(reader, addr, out StressLog? log));
            Assert.NotNull(log);

            int count = 0;
            foreach (StressLogMessage msg in log!.EnumerateMessages())
            {
                count++;
                Assert.Equal(0x42UL, msg.OSThreadId);
                if (count > 10) break;
            }

            Assert.Equal(1, count);
            log.Dispose();
        }

        [Fact]
        public void Layout_x86_AddressOverflowRejected()
        {
            // A chunk whose end address would exceed uint.MaxValue must be
            // rejected; otherwise downstream offset arithmetic could wrap.
            // Place a ThreadStressLog whose chunkListHead points just below
            // 4 GiB so that adding ChunkTotalSize (16400) overflows.
            SyntheticStressLogBuilder builder = new();
            // Override the chunk address to one that overflows 32-bit.
            ulong nearMax = 0xFFFFC000UL;
            HighBitBuilder hb = new(stressLogAddr: 0x100000, threadAddr: 0x200000, chunkAddr: nearMax);
            ulong addr = hb.Build(StressLogLayout.CoreWinX86, out _, out _);
            SyntheticDataReader reader = new(hb.Memory, pointerSize: 4, architecture: System.Runtime.InteropServices.Architecture.X86);

            Assert.True(StressLog.TryOpen(reader, addr, out StressLog? log));
            Assert.NotNull(log);

            int count = 0;
            foreach (StressLogMessage _ in log!.EnumerateMessages())
            {
                count++;
                if (count > 10) break;
            }

            // Chunk overflows 32-bit address space; iterator must reject it.
            Assert.Equal(0, count);
            log.Dispose();
        }

        [Fact]
        public void Layout_x86_RejectsNonWindows()
        {
            SyntheticStressLogBuilder builder = new();
            ulong addr = builder.Build(StressLogLayout.CoreWinX86, out _, out _);
            NonWindowsX86Reader reader = new(builder.Memory);

            Assert.False(StressLog.TryOpen(reader, addr, out StressLog? log, out string? reason));
            Assert.Null(log);
            Assert.NotNull(reason);
            Assert.Contains("32-bit", reason!, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ModuleTable_x86_EntryStrideIs8Bytes()
        {
            // x86 module table: 8 bytes per entry (4 base + 4 size).
            byte[] bytes = new byte[5 * 8];
            // Entry 0: base=0x10000000, size=0x100000 (valid, >= 1 MiB).
            BitConverter.GetBytes((uint)0x10000000).CopyTo(bytes, 0);
            BitConverter.GetBytes((uint)0x100000).CopyTo(bytes, 4);
            // Entries 1..4: zero-filled => skipped.
            StressLogModuleTable table = StressLogModuleTable.BuildInProcess(
                StressLogLayout.CoreWinX86,
                bytes,
                legacyModuleOffset: 0x10000000,
                hasModuleTable: true,
                maxOffset: 1UL << 39);

            Assert.True(table.TryResolveFormatOffset(formatOffset: 0x100, out ulong addr));
            Assert.Equal(0x10000100UL, addr);
        }
    }

    internal sealed class HighBitBuilder
    {
        private readonly SyntheticStressLogBuilder _inner = new();
        private readonly ulong _stressLogAddr;
        private readonly ulong _threadAddr;
        private readonly ulong _chunkAddr;

        public HighBitBuilder(ulong stressLogAddr, ulong threadAddr, ulong chunkAddr)
        {
            _stressLogAddr = stressLogAddr;
            _threadAddr = threadAddr;
            _chunkAddr = chunkAddr;
        }

        public Dictionary<ulong, byte[]> Memory => _inner.Memory;

        public ulong Build(StressLogLayout layout, out ulong threadAddr, out ulong chunkAddr)
        {
            threadAddr = _threadAddr;
            chunkAddr = _chunkAddr;

            byte[] chunk = new byte[layout.ChunkTotalSize];
            WritePtr(layout, chunk.AsSpan(layout.ChunkPrevOffset), _chunkAddr);
            WritePtr(layout, chunk.AsSpan(layout.ChunkNextOffset), _chunkAddr);

            int msgOffset = layout.ChunkBufOffset;
            ulong word0 = 0xABCDEF01UL;
            ulong word1 = 1_500_000UL << StressLogConstants.FormatOffsetHighBits;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(chunk.AsSpan(msgOffset), word0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(chunk.AsSpan(msgOffset + 8), word1);

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(layout.ChunkSig1Offset), StressLogConstants.ChunkSignature);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(layout.ChunkSig2Offset), StressLogConstants.ChunkSignature);

            Memory[_chunkAddr] = chunk;

            byte[] thread = new byte[layout.ThreadHeaderSize];
            if (layout.ThreadIdSize == 8)
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(thread.AsSpan(layout.ThreadIdOffset), 0x42UL);
            else
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(thread.AsSpan(layout.ThreadIdOffset), 0x42u);

            ulong msgAddr = _chunkAddr + (ulong)layout.ChunkBufOffset;
            WritePtr(layout, thread.AsSpan(layout.ThreadCurPtrOffset), msgAddr);
            WritePtr(layout, thread.AsSpan(layout.ThreadReadPtrOffset), msgAddr);
            WritePtr(layout, thread.AsSpan(layout.ThreadChunkListHeadOffset), _chunkAddr);
            WritePtr(layout, thread.AsSpan(layout.ThreadChunkListTailOffset), _chunkAddr);
            WritePtr(layout, thread.AsSpan(layout.ThreadCurReadChunkOffset), _chunkAddr);
            WritePtr(layout, thread.AsSpan(layout.ThreadCurWriteChunkOffset), _chunkAddr);
            Memory[_threadAddr] = thread;

            byte[] header = new byte[layout.InProcHeaderSize];
            WritePtr(layout, header.AsSpan(layout.InProcLogsOffset), _threadAddr);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(layout.InProcPaddingOffset), 0xFFFFFFFFu);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(layout.InProcTickFrequencyOffset), 10_000_000UL);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(layout.InProcStartTimeStampOffset), 1_000_000UL);
            Memory[_stressLogAddr] = header;

            return _stressLogAddr;
        }

        private static void WritePtr(StressLogLayout layout, Span<byte> dst, ulong value)
        {
            if (layout.PointerSize == 8)
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(dst, value);
            else
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(dst, (uint)value);
        }
    }

    internal sealed class NonWindowsX86Reader : IDataReader
    {
        private readonly SyntheticDataReader _inner;
        public NonWindowsX86Reader(Dictionary<ulong, byte[]> segments)
        {
            _inner = new SyntheticDataReader(segments, pointerSize: 4, architecture: System.Runtime.InteropServices.Architecture.X86);
        }
        public string DisplayName => "synthetic-linux-x86";
        public bool IsThreadSafe => true;
        public System.Runtime.InteropServices.OSPlatform TargetPlatform => System.Runtime.InteropServices.OSPlatform.Linux;
        public System.Runtime.InteropServices.Architecture Architecture => System.Runtime.InteropServices.Architecture.X86;
        public int ProcessId => 0;
        public IEnumerable<ModuleInfo> EnumerateModules() => Array.Empty<ModuleInfo>();
        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context) => false;
        public IEnumerable<uint> EnumerateAllThreads() => Array.Empty<uint>();
        public void FlushCachedData() { }
        public int PointerSize => 4;
        public int Read(ulong address, Span<byte> buffer) => _inner.Read(address, buffer);
        public bool Read<T>(ulong address, out T value) where T : unmanaged => _inner.Read(address, out value);
        public T Read<T>(ulong address) where T : unmanaged => _inner.Read<T>(address);
        public bool ReadPointer(ulong address, out ulong value) => _inner.ReadPointer(address, out value);
        public ulong ReadPointer(ulong address) => _inner.ReadPointer(address);
    }
}
