// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace DbgEngExtension
{
    internal static class ExtensionMethods
    {
        public static string ConvertToHumanReadable(this ulong totalBytes) => ConvertToHumanReadable((double)totalBytes);
        public static string ConvertToHumanReadable(this long totalBytes) => ConvertToHumanReadable((double)totalBytes);

        public static string ConvertToHumanReadable(this double totalBytes)
        {
            double updated = totalBytes;

            updated /= 1024;
            if (updated < 1024)
                return $"{updated:0.00}kb";

            updated /= 1024;
            if (updated < 1024)
                return $"{updated:0.00}mb";

            updated /= 1024;
            return $"{updated:0.00}gb";
        }

        public static string[] GetOptionalFlag(this string[] args, string name, out bool value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            if (StartsWithSlash(name))
                throw new ArgumentException($"Do not put a {name[0]} on {nameof(name)}.");

            // There's more efficient ways of doing all of this, but this isn't high performance code
            int i = Array.FindIndex(args, item => item.Length > name.Length && StartsWithSlash(item) && item.AsSpan()[1..] == name);
            if (i > -1)
            {
                value = true;
                return args.Where((_, index) => index != i).ToArray();
            }

            value = false;
            return args;
        }

        private static bool StartsWithSlash(string value) => value.StartsWith('-') || value.StartsWith('/');
    }
}