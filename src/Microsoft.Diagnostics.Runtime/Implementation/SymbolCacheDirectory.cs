// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class SymbolCacheDirectory
    {
        private const uint UserOnlyDirectoryMode = 0x1c0;
        private const int AlreadyExists = 17;

        private const string NoCacheLocationMessage =
            "ClrMD could not determine a per-user symbol cache location.  Set DataTargetOptions.SymbolCachePath " +
            "to a directory that only the current user can modify.";

        private static readonly SymbolCacheDirectory s_default = new(() =>
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify));

        private readonly Func<string> _getRoot;
        private readonly object _sync = new();
        private volatile string? _path;

        internal SymbolCacheDirectory(string root) : this(() => root)
        {
        }

        private SymbolCacheDirectory(Func<string> getRoot)
        {
            _getRoot = getRoot;
        }

        /// <summary>
        /// Returns ClrMD's default symbol cache directory, creating it if needed.  The directory lives
        /// under the current user's local application data folder and is restricted to the current user
        /// on Unix.  ClrMD re-establishes the directory and its permissions before returning it.
        /// </summary>
        public static string GetDefaultPath() => s_default.GetPath();

        internal string GetPath()
        {
            lock (_sync)
            {
                if (_path is null)
                    _path = CreateDefaultPath(_getRoot());
                else
                    EnsureDefaultPath(_path);

                return _path;
            }
        }

        /// <summary>
        /// Ensures that <paramref name="path"/> exists.  ClrMD's default cache is (re)created as a
        /// per-user directory; any other location is caller-configured and is created as given.
        /// </summary>
        public static string EnsureExists(string path)
        {
            SymbolCacheDirectory? directory = GetDefaultForPath(path);
            if (directory is not null)
                return directory.GetPath();

            Directory.CreateDirectory(path);
            return path;
        }

        internal static SymbolCacheDirectory? GetDefaultForPath(string path)
        {
            StringComparison comparison = FileSymbolCache.IsCaseInsensitiveFileSystem ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return s_default._path is not null && string.Equals(path, s_default._path, comparison) ? s_default : null;
        }

        private static string CreateDefaultPath(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException(NoCacheLocationMessage);

            string path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(root, "Microsoft", "ClrMD", "symbols")
                : Path.Combine(root, "Microsoft.ClrMD", "symbols");

            try
            {
                EnsureDefaultPath(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(NoCacheLocationMessage, ex);
            }

            return path;
        }

        private static void EnsureDefaultPath(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Directory.CreateDirectory(path);
                return;
            }

            CreatePrivateDirectory(Path.GetDirectoryName(path)!, restrictExisting: true);
            CreatePrivateDirectory(path, restrictExisting: true);
        }

        private static void CreatePrivateDirectory(string path, bool restrictExisting)
        {
            bool created = false;
            if (!Directory.Exists(path))
            {
                string? parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent))
                    CreatePrivateDirectory(parent, restrictExisting: false);

                int result = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? NativeMethods.MacOS.mkdir(path, UserOnlyDirectoryMode)
                    : NativeMethods.Linux.mkdir(path, UserOnlyDirectoryMode);
                int error = Marshal.GetLastWin32Error();
                if (result != 0 && error != AlreadyExists)
                    throw new IOException($"Could not create the symbol cache directory '{path}': {new Win32Exception(error).Message}");

                created = result == 0;
            }

            if (created || restrictExisting)
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"The symbol cache directory '{path}' must not be a symbolic link.");
                if ((attributes & FileAttributes.Directory) == 0)
                    throw new IOException($"The symbol cache path '{path}' must be a directory.");

                RestrictToCurrentUser(path);
            }
        }

        private static void RestrictToCurrentUser(string path)
        {
            int result = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? NativeMethods.MacOS.chmod(path, UserOnlyDirectoryMode)
                : NativeMethods.Linux.chmod(path, UserOnlyDirectoryMode);

            if (result != 0)
                throw new IOException($"Could not restrict the symbol cache directory '{path}' to the current user: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }

        private static class NativeMethods
        {
            internal static class Linux
            {
                [DllImport("libc", SetLastError = true)]
                internal static extern int mkdir(string pathname, uint mode);

                [DllImport("libc", SetLastError = true)]
                internal static extern int chmod(string pathname, uint mode);
            }

            internal static class MacOS
            {
                [DllImport("libSystem.dylib", SetLastError = true)]
                internal static extern int mkdir(string pathname, uint mode);

                [DllImport("libSystem.dylib", SetLastError = true)]
                internal static extern int chmod(string pathname, uint mode);
            }
        }
    }
}
