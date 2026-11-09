// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.IO;
using Microsoft.Diagnostics.Runtime.MacOS;
using Microsoft.Diagnostics.Runtime.MacOS.Structs;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal sealed class MachFile
    {
        public ImmutableArray<byte> BuildId { get; } = ImmutableArray<byte>.Empty;

        public MachFile(string path)
        {
            using FileStream stream = File.OpenRead(path);
            MachOFileReader reader = new(stream);
            MachHeader64 header = reader.Read<MachHeader64>();
            if (header.Magic != MachHeader64.Magic64)
                throw new InvalidDataException($"File '{path}' does not contain the expected Mach-O header.");

            long end = stream.Position + header.SizeOfCommands;
            if (end > stream.Length)
                throw new InvalidDataException($"Mach-O load commands extend beyond '{path}'.");

            for (uint i = 0; i < header.NumberCommands; i++)
            {
                long offset = stream.Position;
                if (end - offset < LoadCommandHeader.HeaderSize)
                    throw new InvalidDataException($"Truncated Mach-O load command in '{path}'.");

                LoadCommandHeader command = reader.Read<LoadCommandHeader>();
                if (command.Size < LoadCommandHeader.HeaderSize || command.Size > end - offset)
                    throw new InvalidDataException($"Invalid Mach-O load command in '{path}'.");

                if (command.Kind == LoadCommandType.Uuid)
                {
                    if (command.Size < 24)
                        throw new InvalidDataException($"Truncated Mach-O UUID command in '{path}'.");

                    stream.Position = offset;
                    BuildId = reader.Read<UuidLoadCommand>().BuildId.ToImmutableArray();
                    return;
                }

                stream.Position = offset + command.Size;
            }
        }
    }
}