// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.Linux;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class LinuxFunctions : PlatformFunctions
    {
        private const string LibDlGlibc = "libdl.so.2";
        private const string LibDl = "libdl.so";

        private static readonly byte[] s_versionString = Encoding.ASCII.GetBytes("@(#)Version ");
        private static readonly int s_versionLength = s_versionString.Length;

        private readonly Func<string, IntPtr> _loadLibrary;
        private readonly Func<IntPtr, bool> _freeLibrary;
        private readonly Func<IntPtr, string, IntPtr> _getExport;

        private delegate bool TryGetExport(IntPtr handle, string name, out IntPtr address);

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public LinuxFunctions()
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            Type nativeLibraryType = Type.GetType("System.Runtime.InteropServices.NativeLibrary, System.Runtime.InteropServices", throwOnError: false);
            if (nativeLibraryType != null)
            {
                // .NET Core 3.0+
                 var loadLibrary = (Func<string, IntPtr>?)nativeLibraryType.GetMethod("Load", new Type[] { typeof(string) })?.CreateDelegate(typeof(Func<string, IntPtr>));
                if (loadLibrary != null)
                {
                    _loadLibrary = loadLibrary;
                }

                var freeLibrary = (Action<IntPtr>?)nativeLibraryType.GetMethod("Free", new Type[] { typeof(IntPtr) })?.CreateDelegate(typeof(Action<IntPtr>));
                if (freeLibrary != null)
                {
                    _freeLibrary = ptr => { freeLibrary(ptr); return true; };
                }

                var tryGetExport = (TryGetExport?)nativeLibraryType.GetMethod("TryGetExport", new Type[] { typeof(IntPtr), typeof(string), typeof(IntPtr).MakeByRefType() })
                    ?.CreateDelegate(typeof(TryGetExport));
                if (tryGetExport != null)
                {
                    _getExport = (IntPtr handle, string name) =>
                    {
                        tryGetExport(handle, name, out IntPtr address);
                        return address;
                    };
                }
            }

            if (_loadLibrary is null ||
                _freeLibrary is null ||
                _getExport is null)
            {
                // On glibc based Linux distributions, 'libdl.so' is a symlink provided by development packages.
                // To work on production machines, we fall back to 'libdl.so.2' which is the actual library name.
                bool useGlibcDl = false;
                try
                {
                    dlopen("/", 0);
                }
                catch (DllNotFoundException)
                {
                    try
                    {
                        dlopen_glibc("/", 0);
                        useGlibcDl = true;
                    }
                    catch (DllNotFoundException)
                    { }
                }

                if (useGlibcDl)
                {
                    _loadLibrary = filename => dlopen_glibc(filename, RTLD_NOW);
                    _freeLibrary = ptr => dlclose_glibc(ptr) == 0;
                    _getExport = dlsym_glibc;
                }
                else
                {
                    _loadLibrary = filename => dlopen(filename, RTLD_NOW);
                    _freeLibrary = ptr => dlclose(ptr) == 0;
                    _getExport = dlsym;
                }
            }
        }

        internal static void GetVersionInfo(IDataReader dataReader, ulong baseAddress, ElfFile loadedFile, out VersionInfo version)
        {
            foreach (ElfProgramHeader programHeader in loadedFile.ProgramHeaders)
            {
                if (programHeader.Type == ElfProgramHeaderType.Load && programHeader.IsWritable)
                {
                    long loadAddress = programHeader.VirtualAddress;
                    long loadSize = programHeader.VirtualSize;
                    GetVersionInfo(dataReader, baseAddress + (ulong)loadAddress, (ulong)loadSize, out version);
                    return;
                }
            }

            version = default;
        }

        internal static unsafe void GetVersionInfo(IDataReader dataReader, ulong address, ulong size, out VersionInfo version)
        {
            Span<byte> buffer = stackalloc byte[s_versionLength];
            ulong endAddress = address + size;

            while (address < endAddress)
            {
                bool result = dataReader.ReadMemory(address, buffer, out int read);
                if (!result || read < s_versionLength)
                {
                    address += (uint)s_versionLength;
                    continue;
                }

                if (!buffer.SequenceEqual(s_versionString))
                {
                    address++;
                    continue;
                }

                address += (uint)s_versionLength;

                StringBuilder builder = new StringBuilder();
                while (address < endAddress)
                {
                    Span<byte> bytes = stackalloc byte[1];
                    result = dataReader.ReadMemory(address, bytes, out read);
                    if (!result || read < bytes.Length)
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
                            version = new VersionInfo(v.Major, v.Minor, v.Build, v.Revision);
                            return;
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

            version = default;
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

            Debug.Assert(dataOffset != 0);
            Debug.Assert(dataSize != 0);

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

        public override IntPtr LoadLibrary(string filename)
            => _loadLibrary(filename);

        public override bool FreeLibrary(IntPtr module)
            => _freeLibrary(module);

        public override IntPtr GetProcAddress(IntPtr module, string method)
            => _getExport(module, method);

        [DllImport(LibDlGlibc, EntryPoint = nameof(dlopen))]
        private static extern IntPtr dlopen_glibc(string filename, int flags);

        [DllImport(LibDlGlibc, EntryPoint = nameof(dlclose))]
        private static extern int dlclose_glibc(IntPtr module);

        [DllImport(LibDlGlibc, EntryPoint = nameof(dlsym))]
        private static extern IntPtr dlsym_glibc(IntPtr handle, string symbol);

        [DllImport(LibDl)]
        private static extern IntPtr dlopen(string filename, int flags);
        [DllImport(LibDl)]
        private static extern int dlclose(IntPtr module);
        [DllImport(LibDl)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libc")]
        public static extern int symlink(string file, string symlink);

        private const int RTLD_NOW = 2;
    }
}