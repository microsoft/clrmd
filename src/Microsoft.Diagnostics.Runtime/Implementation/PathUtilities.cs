// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal static class PathUtilities
    {
        private static readonly char[] s_directorySeparators = ['\\', '/'];

        public static string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            int index = path.LastIndexOfAny(s_directorySeparators);
            return index >= 0 ? path.Substring(index + 1) : path;
        }

        public static bool IsRemoteOrDevicePath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string nonNullPath = path!;
            return StartsWithTwoDirectorySeparators(nonNullPath);
        }

        public static bool IsSafeAbsoluteLocalPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string nonNullPath = path!;
            if (StartsWithTwoDirectorySeparators(nonNullPath))
                return false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return IsWindowsDriveAbsolutePath(nonNullPath);

            return nonNullPath[0] == '/';
        }

        private static bool StartsWithTwoDirectorySeparators(string path)
        {
            return path.Length >= 2 && IsDirectorySeparator(path[0]) && IsDirectorySeparator(path[1]);
        }

        private static bool IsWindowsDriveAbsolutePath(string path)
        {
            return path.Length >= 3 && IsAsciiLetter(path[0]) && path[1] == ':' && IsDirectorySeparator(path[2]);
        }

        private static bool IsDirectorySeparator(char c)
        {
            return c is '\\' or '/';
        }

        private static bool IsAsciiLetter(char c)
        {
            return c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
        }
    }
}
