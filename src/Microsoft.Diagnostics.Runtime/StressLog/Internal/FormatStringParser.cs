// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Parses a sanitized printf-style format string and dispatches typed
    /// tokens to a <see cref="IStressLogFormatReceiver"/>. The parser
    /// recognizes a strict allow-list of conversion specifiers and rejects
    /// every other specifier including <c>%n</c>, positional specifiers
    /// (<c>%1$s</c>), and indirect width/precision (<c>%*d</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The input is bytes that have already been read and sanitized via
    /// <see cref="StressLogSanitizer"/>. Sanitization preserves <c>%</c>
    /// (since <c>%</c> is in the printable ASCII range), so format
    /// specifiers survive sanitization intact.
    /// </para>
    /// <para>
    /// Width and precision are clamped to a small constant before being
    /// surfaced. Length modifiers (<c>h</c>, <c>l</c>, <c>ll</c>, <c>z</c>,
    /// <c>j</c>, <c>t</c>, <c>L</c>, <c>I</c>, <c>I32</c>, <c>I64</c>) are
    /// recognized and discarded; the runtime stores every argument
    /// pointer-sized, so the modifier does not affect decoding.
    /// </para>
    /// </remarks>
    internal static class FormatStringParser
    {
        private const int MaxWidth = 256;
        private const int MaxPrecision = 256;

        public static void Parse<TReceiver>(
            ReadOnlySpan<byte> format,
            ReadOnlySpan<ulong> args,
            ArgumentResolver argumentResolver,
            ref TReceiver receiver)
            where TReceiver : struct, IStressLogFormatReceiver
        {
            int argIndex = 0;
            int literalStart = 0;
            int i = 0;

            while (i < format.Length)
            {
                byte b = format[i];
                if (b != (byte)'%')
                {
                    i++;
                    continue;
                }

                // Flush accumulated literal bytes, if any.
                if (i > literalStart)
                    receiver.Literal(format.Slice(literalStart, i - literalStart));

                int specStart = i;
                i++; // consume '%'

                if (i >= format.Length)
                {
                    // Trailing lone '%'. Render as literal '%'.
                    receiver.Literal(format.Slice(specStart, 1));
                    literalStart = format.Length;
                    break;
                }

                // %% -> literal '%'.
                if (format[i] == (byte)'%')
                {
                    receiver.Literal(format.Slice(specStart + 1, 1));
                    i++;
                    literalStart = i;
                    continue;
                }

                if (!TryParseSpec(format, ref i, out FormatSpec spec))
                {
                    // Unknown / disallowed specifier (incl. %n, positional).
                    // Render the raw spec bytes as a literal for traceability;
                    // they were sanitized, so they cannot escape into a
                    // downstream formatter.
                    receiver.Literal(format.Slice(specStart, i - specStart));
                    literalStart = i;
                    continue;
                }

                DispatchSpec(spec, args, ref argIndex, argumentResolver, ref receiver);
                literalStart = i;
            }

            if (literalStart < format.Length)
                receiver.Literal(format.Slice(literalStart));
        }

        private static bool TryParseSpec(ReadOnlySpan<byte> format, ref int i, out FormatSpec spec)
        {
            spec = default;
            int start = i;

            // Reject positional arguments: %<digits>$.
            int probe = i;
            while (probe < format.Length && format[probe] is >= (byte)'0' and <= (byte)'9')
                probe++;
            if (probe < format.Length && format[probe] == (byte)'$')
            {
                i = probe + 1;
                return false;
            }

            // Flags.
            while (i < format.Length)
            {
                byte b = format[i];
                if (b is (byte)'-' or (byte)'+' or (byte)' ' or (byte)'#' or (byte)'0')
                    i++;
                else
                    break;
            }

            // Width.
            int width = -1;
            if (i < format.Length && format[i] == (byte)'*')
            {
                // Indirect width pulls from an argument; we do not support this.
                i++;
                return false;
            }

            if (i < format.Length && format[i] is >= (byte)'0' and <= (byte)'9')
            {
                width = 0;
                while (i < format.Length && format[i] is (byte)'0' or (byte)'1' or (byte)'2' or (byte)'3' or (byte)'4'
                                                       or (byte)'5' or (byte)'6' or (byte)'7' or (byte)'8' or (byte)'9')
                {
                    int digit = format[i] - (byte)'0';
                    if (width <= MaxWidth)
                        width = width * 10 + digit;
                    i++;
                }
                if (width > MaxWidth)
                    width = MaxWidth;
            }

            // Precision.
            int precision = -1;
            if (i < format.Length && format[i] == (byte)'.')
            {
                i++;
                if (i < format.Length && format[i] == (byte)'*')
                {
                    i++;
                    return false;
                }

                precision = 0;
                while (i < format.Length && format[i] is (byte)'0' or (byte)'1' or (byte)'2' or (byte)'3' or (byte)'4'
                                                       or (byte)'5' or (byte)'6' or (byte)'7' or (byte)'8' or (byte)'9')
                {
                    int digit = format[i] - (byte)'0';
                    if (precision <= MaxPrecision)
                        precision = precision * 10 + digit;
                    i++;
                }
                if (precision > MaxPrecision)
                    precision = MaxPrecision;
            }

            // Length modifiers (recognized).
            // h, hh, l, ll, z, j, t, L, I, I32, I64.
            // ll, j, L, I64 indicate 64-bit args; on 32-bit targets this
            // means the argument occupies two consecutive arg slots (low
            // dword followed by high dword), so we record the modifier so
            // that DispatchSpec can combine slots when needed.
            bool is64Bit = false;
            if (i < format.Length)
            {
                byte b = format[i];
                if (b == (byte)'h')
                {
                    i++;
                    if (i < format.Length && format[i] == (byte)'h') i++;
                }
                else if (b == (byte)'l')
                {
                    i++;
                    if (i < format.Length && format[i] == (byte)'l')
                    {
                        is64Bit = true;
                        i++;
                    }
                }
                else if (b is (byte)'z' or (byte)'t')
                {
                    i++;
                }
                else if (b is (byte)'j' or (byte)'L')
                {
                    is64Bit = true;
                    i++;
                }
                else if (b == (byte)'I')
                {
                    i++;
                    if (i + 1 < format.Length && format[i] == (byte)'3' && format[i + 1] == (byte)'2')
                        i += 2;
                    else if (i + 1 < format.Length && format[i] == (byte)'6' && format[i + 1] == (byte)'4')
                    {
                        is64Bit = true;
                        i += 2;
                    }
                }
            }

            // Conversion specifier.
            if (i >= format.Length)
                return false;

            byte conv = format[i];
            i++;

            switch (conv)
            {
                case (byte)'d':
                case (byte)'i':
                    spec = new FormatSpec(SpecKind.Integer, StressLogIntegerKind.Decimal, default, default, width, precision, is64Bit);
                    return true;
                case (byte)'u':
                    spec = new FormatSpec(SpecKind.Integer, StressLogIntegerKind.Unsigned, default, default, width, precision, is64Bit);
                    return true;
                case (byte)'x':
                    spec = new FormatSpec(SpecKind.Integer, StressLogIntegerKind.Hex, default, default, width, precision, is64Bit);
                    return true;
                case (byte)'X':
                    spec = new FormatSpec(SpecKind.Integer, StressLogIntegerKind.HexUpper, default, default, width, precision, is64Bit);
                    return true;
                case (byte)'c':
                    spec = new FormatSpec(SpecKind.Integer, StressLogIntegerKind.Char, default, default, width, precision, is64Bit);
                    return true;

                case (byte)'p':
                    // %p, optionally followed by a single letter that selects a
                    // runtime pointer kind: %pM / %pT / %pV / %pK.
                    StressLogPointerKind ptrKind = StressLogPointerKind.Plain;
                    if (i < format.Length)
                    {
                        byte sub = format[i];
                        switch (sub)
                        {
                            case (byte)'M': ptrKind = StressLogPointerKind.MethodDesc; i++; break;
                            case (byte)'T': ptrKind = StressLogPointerKind.MethodTable; i++; break;
                            case (byte)'V': ptrKind = StressLogPointerKind.VTable; i++; break;
                            case (byte)'K': ptrKind = StressLogPointerKind.CodePointer; i++; break;
                            default: break;
                        }
                    }
                    spec = new FormatSpec(SpecKind.Pointer, default, ptrKind, default, width, precision, false);
                    return true;

                case (byte)'s':
                    spec = new FormatSpec(SpecKind.String, default, default, StressLogStringEncoding.Utf8, width, precision, false);
                    return true;
                case (byte)'S':
                    spec = new FormatSpec(SpecKind.String, default, default, StressLogStringEncoding.Utf16, width, precision, false);
                    return true;

                default:
                    // %n, %a, %e, %f, %g, %o, %v, ... all rejected.
                    _ = start; // start is used only for callers wanting to report the rejected range.
                    return false;
            }
        }

        private static void DispatchSpec<TReceiver>(
            FormatSpec spec,
            ReadOnlySpan<ulong> args,
            ref int argIndex,
            ArgumentResolver argumentResolver,
            ref TReceiver receiver)
            where TReceiver : struct, IStressLogFormatReceiver
        {
            if ((uint)argIndex >= (uint)args.Length)
            {
                receiver.MissingArgument();
                return;
            }

            ulong raw = args[argIndex++];

            // On 32-bit targets, a 64-bit length-modified argument
            // (ll/I64/j/L) occupies two pointer-sized slots: low dword
            // first, then high dword. Combine them so the receiver sees
            // the full 64-bit value. On 64-bit targets each slot is
            // already 8 bytes wide so no combination is needed.
            if (spec.Is64Bit && argumentResolver.PointerSize == 4)
            {
                if ((uint)argIndex < (uint)args.Length)
                {
                    ulong hi = args[argIndex++];
                    raw = (raw & 0xFFFFFFFFUL) | (hi << 32);
                }
            }

            switch (spec.Kind)
            {
                case SpecKind.Integer:
                    {
                        // Apply the printf rule that an unmodified %d/%i/%u/%x
                        // refers to a 32-bit int. The runtime stores each
                        // argument size_t-wide; on 32-bit targets that's
                        // already 4 bytes (zero-extended into the 8-byte
                        // slot), and on 64-bit targets a 32-bit value was
                        // sign- or zero-extended via implicit conversion at
                        // store time. Truncate-then-extend through int/uint
                        // to recover the value the caller wrote.
                        long signedValue;
                        if (spec.Is64Bit)
                        {
                            signedValue = unchecked((long)raw);
                        }
                        else if (spec.IntegerKind == StressLogIntegerKind.Decimal)
                        {
                            signedValue = unchecked((int)raw);
                        }
                        else
                        {
                            signedValue = unchecked((long)(uint)raw);
                        }
                        receiver.Integer(spec.IntegerKind, spec.Width, spec.Precision, signedValue);
                        break;
                    }

                case SpecKind.Pointer:
                    receiver.Pointer(spec.PointerKind, raw);
                    break;

                case SpecKind.String:
                    {
                        ReadOnlySpan<byte> sanitized = argumentResolver.ResolveString(raw, spec.StringEncoding);
                        receiver.String(spec.StringEncoding, sanitized);
                        break;
                    }
            }
        }

        private enum SpecKind
        {
            Integer,
            Pointer,
            String,
        }

        private readonly struct FormatSpec
        {
            public FormatSpec(SpecKind kind,
                              StressLogIntegerKind integerKind,
                              StressLogPointerKind pointerKind,
                              StressLogStringEncoding stringEncoding,
                              int width,
                              int precision,
                              bool is64Bit)
            {
                Kind = kind;
                IntegerKind = integerKind;
                PointerKind = pointerKind;
                StringEncoding = stringEncoding;
                Width = width;
                Precision = precision;
                Is64Bit = is64Bit;
            }

            public SpecKind Kind { get; }
            public StressLogIntegerKind IntegerKind { get; }
            public StressLogPointerKind PointerKind { get; }
            public StressLogStringEncoding StringEncoding { get; }
            public int Width { get; }
            public int Precision { get; }
            public bool Is64Bit { get; }
        }
    }
}
