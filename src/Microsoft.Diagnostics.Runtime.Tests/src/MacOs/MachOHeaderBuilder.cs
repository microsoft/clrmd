// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Tests.MacOs;

internal sealed class MachOHeaderBuilder
{
    private const uint MachHeader64Size = 32;
    private const uint Segment64Command = 0x19;
    private const uint Segment64CommandSize = 72;
    private const uint SymbolTableCommand = 0x02;
    private const uint SymbolTableCommandSize = 24;
    private const uint DynamicSymbolTableCommand = 0x0b;
    private const uint DynamicSymbolTableCommandSize = 80;
    private const uint DyldInfoOnlyCommand = 0x80000022;
    private const uint DyldInfoCommandSize = 48;
    private const uint ExportsTrieCommand = 0x80000033;
    private const uint LinkeditDataCommandSize = 16;

    private readonly List<LoadCommand> _commands = [];

    public MachOHeaderBuilder AddSegment(
        string name,
        ulong vmAddress,
        ulong fileOffset,
        ulong fileSize,
        ulong? vmSize = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        byte[] nameBytes = Encoding.ASCII.GetBytes(name);
        if (nameBytes.Length > 16)
        {
            throw new ArgumentException("A Mach-O segment name cannot exceed 16 bytes.", nameof(name));
        }

        _commands.Add(new LoadCommand(Segment64CommandSize, writer =>
        {
            writer.Write(Segment64Command);
            writer.Write(Segment64CommandSize);

            byte[] paddedName = new byte[16];
            nameBytes.CopyTo(paddedName, 0);
            writer.Write(paddedName);
            writer.Write(vmAddress);
            writer.Write(vmSize ?? fileSize);
            writer.Write(fileOffset);
            writer.Write(fileSize);
            writer.Write(7U);
            writer.Write(5U);
            writer.Write(0U);
            writer.Write(0U);
        }));
        return this;
    }

    public MachOHeaderBuilder AddExportsTrie(uint fileOffset, uint size)
    {
        _commands.Add(new LoadCommand(LinkeditDataCommandSize, writer =>
        {
            writer.Write(ExportsTrieCommand);
            writer.Write(LinkeditDataCommandSize);
            writer.Write(fileOffset);
            writer.Write(size);
        }));
        return this;
    }

    public MachOHeaderBuilder AddDyldInfoExports(uint fileOffset, uint size)
    {
        _commands.Add(new LoadCommand(DyldInfoCommandSize, writer =>
        {
            writer.Write(DyldInfoOnlyCommand);
            writer.Write(DyldInfoCommandSize);
            for (int i = 0; i < 8; i++)
            {
                writer.Write(0U);
            }

            writer.Write(fileOffset);
            writer.Write(size);
        }));
        return this;
    }

    public MachOHeaderBuilder AddSymbolTable(
        uint symbolTableOffset,
        uint symbolCount,
        uint stringTableOffset,
        uint stringTableSize)
    {
        _commands.Add(new LoadCommand(SymbolTableCommandSize, writer =>
        {
            writer.Write(SymbolTableCommand);
            writer.Write(SymbolTableCommandSize);
            writer.Write(symbolTableOffset);
            writer.Write(symbolCount);
            writer.Write(stringTableOffset);
            writer.Write(stringTableSize);
        }));
        return this;
    }

    public MachOHeaderBuilder AddDynamicSymbolTable(uint externalDefinitionsCount)
    {
        _commands.Add(new LoadCommand(DynamicSymbolTableCommandSize, writer =>
        {
            writer.Write(DynamicSymbolTableCommand);
            writer.Write(DynamicSymbolTableCommandSize);
            writer.Write(0U); // ilocalsym
            writer.Write(0U); // nlocalsym
            writer.Write(0U); // iextdefsym
            writer.Write(externalDefinitionsCount);
            for (int i = 0; i < 14; i++)
            {
                writer.Write(0U);
            }
        }));
        return this;
    }

    public byte[] Build()
    {
        uint sizeOfCommands = checked((uint)_commands.Sum(command => command.Size));
        using MemoryStream stream = new(checked((int)(MachHeader64Size + sizeOfCommands)));
        using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(0xfeedfacfU);
        writer.Write(0x01000007);
        writer.Write(3);
        writer.Write(2U);
        writer.Write(checked((uint)_commands.Count));
        writer.Write(sizeOfCommands);
        writer.Write(0U);
        writer.Write(0U);

        foreach (LoadCommand command in _commands)
        {
            command.Write(writer);
        }

        return stream.ToArray();
    }

    private sealed class LoadCommand(uint size, Action<BinaryWriter> write)
    {
        public int Size { get; } = checked((int)size);

        public void Write(BinaryWriter writer) => write(writer);
    }
}