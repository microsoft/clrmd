// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.Linux;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class LinuxFunctions : CoreFunctions
    {
#if !NETCOREAPP3_1
        private readonly Func<string, int, IntPtr> _dlopen;
        private readonly Func<IntPtr> _dlerror;
        private readonly Func<IntPtr, int> _dlclose;
        private readonly Func<IntPtr, string, IntPtr> _dlsym;

        public LinuxFunctions()
        {
            // On glibc based Linux distributions, 'libdl.so' is a symlink provided by development packages.
            // To work on production machines, we fall back to 'libdl.so.2' which is the actual library name.
            bool useGlibcDl = false;
            try
            {
                NativeMethods.dlopen("/", 0);
            }
            catch (DllNotFoundException)
            {
                try
                {
                    NativeMethods.dlopen_glibc("/", 0);
                    useGlibcDl = true;
                }
                catch (DllNotFoundException)
                {
                }
            }

            if (useGlibcDl)
            {
                _dlopen = NativeMethods.dlopen_glibc;
                _dlerror = NativeMethods.dlerror_glibc;
                _dlclose = NativeMethods.dlclose_glibc;
                _dlsym = NativeMethods.dlsym_glibc;
            }
            else
            {
                _dlopen = NativeMethods.dlopen;
                _dlerror = NativeMethods.dlerror;
                _dlclose = NativeMethods.dlclose;
                _dlsym = NativeMethods.dlsym;
            }
        }
#endif

        internal static bool GetVersionInfo(IDataReader dataReader, ulong baseAddress, ElfFile loadedFile, out VersionInfo version)
        {
            foreach (ElfProgramHeader programHeader in loadedFile.ProgramHeaders)
            {
                if (programHeader.Type == ElfProgramHeaderType.Load && programHeader.IsWritable)
                {
                    long loadAddress = programHeader.VirtualAddress;
                    long loadSize = programHeader.VirtualSize;
                    return GetVersionInfo(dataReader, baseAddress + (ulong)loadAddress, (ulong)loadSize, out version);
                }
            }

            version = default;
            return false;
        }

        internal static unsafe bool GetVersionInfo(IDataReader dataReader, ulong startAddress, ulong size, out VersionInfo version)
        {
            version = default;

            // (int)size underflow will result in returning 0 here, so this is acceptable
            ulong address = dataReader.SearchMemory(startAddress, (int)size, s_versionString);
            if (address == 0)
                return false;

            Span<byte> bytes = stackalloc byte[64];
            int read = dataReader.Read(address + (uint)s_versionString.Length, bytes);
            if (read > 0)
            {
                bytes = bytes.Slice(0, read);
                version = ParseAsciiVersion(bytes);
                return true;
            }

            return false;
        }

        private static VersionInfo ParseAsciiVersion(ReadOnlySpan<byte> span)
        {
            int major = 0, minor = 0, rev = 0, patch = 0;

            int position = 0;
            long curr = 0;

            for (int i = 0; ; i++)
            {
                if (i == span.Length || span[i] == '.' || span[i] == ' ')
                {
                    switch (position)
                    {
                        case 0:
                            major = (int)curr;
                            break;

                        case 1:
                            minor = (int)curr;
                            break;

                        case 2:
                            rev = (int)curr;
                            break;

                        case 3:
                            patch = (int)curr;
                            break;
                    }

                    curr = 0;
                    if (i == span.Length)
                        break;

                    if (++position == 4 || span[i] == ' ')
                        break;
                }

                // skip bits like "-beta"
                if ('0' <= span[i] && span[i] <= '9')
                    curr = curr * 10 + (span[i] - '0');

                // In this case I don't know what we are parsing but it's not a version
                if (curr > int.MaxValue)
                    return default;
            }

            return new VersionInfo(major, minor, rev, patch, true);
        }

        internal override unsafe bool GetFileVersion(string dll, out int major, out int minor, out int revision, out int patch)
        {
            using FileStream stream = File.OpenRead(dll);
            StreamAddressSpace streamAddressSpace = new StreamAddressSpace(stream);
            Reader streamReader = new Reader(streamAddressSpace);
            ElfFile file = new ElfFile(streamReader);
            IElfHeader header = file.Header;

            ElfSectionHeader headerStringHeader = new ElfSectionHeader(streamReader, header.Is64Bit, header.SectionHeaderOffset + header.SectionHeaderStringIndex * header.SectionHeaderEntrySize);
            long headerStringOffset = (long)headerStringHeader.FileOffset;

            long dataOffset = 0;
            long dataSize = 0;
            for (int i = 0; i < header.SectionHeaderCount; i++)
            {
                if (i == header.SectionHeaderStringIndex)
                {
                    continue;
                }

                ElfSectionHeader sectionHeader = new ElfSectionHeader(streamReader, header.Is64Bit, header.SectionHeaderOffset + i * header.SectionHeaderEntrySize);
                if (sectionHeader.Type == ElfSectionHeaderType.ProgBits)
                {
                    string sectionName = streamReader.ReadNullTerminatedAscii(headerStringOffset + sectionHeader.NameIndex * sizeof(byte));
                    if (sectionName == ".data")
                    {
                        dataOffset = (long)sectionHeader.FileOffset;
                        dataSize = (long)sectionHeader.FileSize;
                        break;
                    }
                }
            }

            DebugOnly.Assert(dataOffset != 0);
            DebugOnly.Assert(dataSize != 0);

            Span<byte> buffer = stackalloc byte[s_versionLength];
            long address = dataOffset;
            long endAddress = address + dataSize;

            while (address < endAddress)
            {
                int read = streamAddressSpace.Read(address, buffer);
                if (read < s_versionLength)
                {
                    break;
                }

                if (!buffer.SequenceEqual(s_versionString))
                {
                    address++;
                    continue;
                }

                address += s_versionLength;

                // TODO:  This should be cleaned up to not read byte by byte in the future.  Leaving it here
                // until we decide whether to rewrite the Linux coredumpreader or not.
                StringBuilder builder = new StringBuilder();
                while (address < endAddress)
                {
                    Span<byte> bytes = stackalloc byte[1];
                    read = streamAddressSpace.Read(address, bytes);
                    if (read < bytes.Length)
                    {
                        break;
                    }

                    if (bytes[0] == '\0')
                    {
                        break;
                    }

                    if (bytes[0] == ' ')
                    {
                        try
                        {
                            Version v = Version.Parse(builder.ToString());
                            major = v.Major;
                            minor = v.Minor;
                            revision = v.Build;
                            patch = v.Revision;
                            return true;
                        }
                        catch (FormatException)
                        {
                            break;
                        }
                    }

                    Span<char> chars = stackalloc char[1];
                    fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
                    fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
                    {
                        _ = Encoding.ASCII.GetChars(bytesPtr, bytes.Length, charsPtr, chars.Length);
                    }

                    _ = builder.Append(chars[0]);
                    address++;
                }

                break;
            }

            major = minor = revision = patch = 0;
            return false;
        }

        public override bool TryGetWow64(IntPtr proc, out bool result)
        {
            result = false;
            return true;
        }

#if !NETCOREAPP3_1
        public override IntPtr LoadLibrary(string libraryPath)
        {
            IntPtr handle = _dlopen(libraryPath, NativeMethods.RTLD_NOW);
            if (handle == IntPtr.Zero)
                throw new DllNotFoundException(Marshal.PtrToStringAnsi(_dlerror()));

            return handle;
        }

        public override bool FreeLibrary(IntPtr handle)
        {
            return _dlclose(handle) == 0;
        }

        public override IntPtr GetProcAddress(IntPtr handle, string name)
        {
            return _dlsym(handle, name);
        }

        internal static class NativeMethods
        {
            private const string LibDlGlibc = "libdl.so.2";
            private const string LibDl = "libdl.so";

            internal const int RTLD_NOW = 2;

            [DllImport(LibDlGlibc, EntryPoint = nameof(dlopen))]
            internal static extern IntPtr dlopen_glibc(string fileName, int flags);

            [DllImport(LibDlGlibc, EntryPoint = nameof(dlerror))]
            internal static extern IntPtr dlerror_glibc();

            [DllImport(LibDlGlibc, EntryPoint = nameof(dlclose))]
            internal static extern int dlclose_glibc(IntPtr handle);

            [DllImport(LibDlGlibc, EntryPoint = nameof(dlsym))]
            internal static extern IntPtr dlsym_glibc(IntPtr handle, string symbol);

            [DllImport(LibDl)]
            internal static extern IntPtr dlopen(string fileName, int flags);

            [DllImport(LibDl)]
            internal static extern IntPtr dlerror();

            [DllImport(LibDl)]
            internal static extern int dlclose(IntPtr handle);

            [DllImport(LibDl)]
            internal static extern IntPtr dlsym(IntPtr handle, string symbol);
        }
#endif

        [DllImport("libc")]
        public static extern int symlink(string file, string symlink);
    }
}