// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Tests.MacOs;

internal sealed class MachOExport
{
    private const ulong KindAbsolute = 0x02;
    private const ulong WeakDefinition = 0x04;
    private const ulong Reexport = 0x08;
    private const ulong StubAndResolverFlag = 0x10;

    private readonly byte[] _terminalData;

    private MachOExport(byte[] terminalData)
    {
        _terminalData = terminalData;
    }

    public static MachOExport Regular(ulong address, params byte[] trailingData)
        => Create(flags: 0, address, trailingData);

    public static MachOExport Absolute(ulong address)
        => Create(KindAbsolute, address);

    public static MachOExport Weak(ulong address)
        => Create(WeakDefinition, address);

    public static MachOExport StubAndResolver(ulong address, ulong resolver)
        => Create(StubAndResolverFlag, address, additionalValues: [resolver]);

    public static MachOExport Reexported(ulong ordinal, string importedName = "")
    {
        ArgumentNullException.ThrowIfNull(importedName);
        if (importedName.Contains('\0'))
            throw new ArgumentException("A reexported symbol name cannot contain a null character.", nameof(importedName));

        using MemoryStream stream = new();
        WriteUleb128(stream, Reexport);
        WriteUleb128(stream, ordinal);
        stream.Write(Encoding.UTF8.GetBytes(importedName));
        stream.WriteByte(0);
        return new MachOExport(stream.ToArray());
    }

    public static MachOExport Raw(ulong flags, ulong address, params ulong[] additionalValues)
        => Create(flags, address, additionalValues: additionalValues);

    internal byte[] GetTerminalData() => _terminalData.ToArray();

    private static MachOExport Create(
        ulong flags,
        ulong address,
        byte[]? trailingData = null,
        ulong[]? additionalValues = null)
    {
        using MemoryStream stream = new();
        WriteUleb128(stream, flags);
        WriteUleb128(stream, address);
        if (additionalValues is not null)
        {
            foreach (ulong value in additionalValues)
            {
                WriteUleb128(stream, value);
            }
        }

        if (trailingData is not null)
        {
            stream.Write(trailingData);
        }

        return new MachOExport(stream.ToArray());
    }

    private static void WriteUleb128(Stream stream, ulong value)
    {
        do
        {
            byte next = (byte)(value & 0x7f);
            value >>= 7;
            if (value != 0)
                next |= 0x80;

            stream.WriteByte(next);
        }
        while (value != 0);
    }
}