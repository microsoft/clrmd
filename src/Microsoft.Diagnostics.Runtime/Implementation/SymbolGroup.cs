// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Azure.Core;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class SymbolGroup : IFileLocator
    {
        private static readonly string s_defaultCacheLocation = Path.Combine(Path.GetTempPath(), "symbols");
        private static volatile FileSymbolCache? s_cache;


        private readonly ImmutableArray<IFileLocator> _groups;

        public SymbolGroup(IEnumerable<IFileLocator> groups)
        {
            _groups = groups.ToImmutableArray();
        }

        public string? FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            foreach (IFileLocator locator in _groups)
            {
                string? result = locator.FindPEImage(fileName, buildTimeStamp, imageSize, checkProperties);
                if (result != null)
                    return result;
            }

            return null;
        }

        public string? FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties)
        {
            foreach (IFileLocator locator in _groups)
            {
                string? result = locator.FindPEImage(fileName, archivedUnder, buildIdOrUUID, originalPlatform, checkProperties);
                if (result != null)
                    return result;
            }

            return null;
        }

        public string? FindElfImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildId, bool checkProperties)
        {
            foreach (IFileLocator locator in _groups)
            {
                string? result = locator.FindElfImage(fileName, archivedUnder, buildId, checkProperties);
                if (result != null)
                    return result;
            }

            return null;
        }

        public string? FindMachOImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> uuid, bool checkProperties)
        {
            foreach (IFileLocator locator in _groups)
            {
                string? result = locator.FindMachOImage(fileName, archivedUnder, uuid, checkProperties);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static FileSymbolCache GetDefaultCache()
        {
            FileSymbolCache? cache = s_cache;
            if (cache != null)
                return cache;

            // We always expect to be able to create a temporary directory
            Directory.CreateDirectory(s_defaultCacheLocation);
            cache = new FileSymbolCache(s_defaultCacheLocation);

            Interlocked.CompareExchange(ref s_cache, cache, null);
            return s_cache!;
        }

        public static IFileLocator CreateFromSymbolPath(string symbolPath, bool trace, TokenCredential? credential)
        {
            FileSymbolCache defaultCache = GetDefaultCache();
            List<IFileLocator> locators = new();

            bool first = false;
            SymbolServer? single = null;

            foreach ((string? Cache, string[] Servers) in symbolPath.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(EnumerateEntries))
            {
                if (Servers.Length == 0)
                    continue;

                FileSymbolCache cache = defaultCache;
                if (Cache != null && !defaultCache.Location.Equals(Cache, FileSymbolCache.IsCaseInsensitiveFileSystem ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(Cache);
                    cache = new FileSymbolCache(Cache);

                    // if the cache is not the default, we have to add it to the list of locators so we check there before hitting the symbol server
                    locators.Add(cache);
                }

                foreach (string server in Servers)
                {
                    if (IsUrl(server))
                    {
                        SymbolServer symSvr = new(cache, server, trace, credential);
                        locators.Add(symSvr);

                        if (first)
                        {
                            single = symSvr;
                            first = false;
                        }
                        else
                        {
                            single = null;
                        }
                    }
                    else if (Directory.Exists(server))
                    {
                        locators.Add(new FileSymbolCache(server));
                    }
                    else
                    {
                        Trace.WriteLine($"Ignoring symbol part: {server}");
                    }
                }
            }

            if (single != null)
                return single;

            if (locators.Count == 0)
                return new SymbolServer(defaultCache, SymbolServer.Msdl, trace, null);

            return new SymbolGroup(locators);
        }

        internal static (string? Cache, string[] Servers) EnumerateEntries(string part)
        {
            string? cache = null;
            List<string> servers = [];
            foreach (string entry in EnumerateParts(part))
            {
                if (cache is null && servers.Count == 0 && !IsUrl(entry))
                    cache = entry;
                else
                    servers.Add(entry);
            }

            return (cache, servers.ToArray());
        }

        private static IEnumerable<string> EnumerateParts(string path)
        {
            bool possiblySkipNextDll = false;
            int curr = 0;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '*')
                {
                    ReadOnlySpan<char> part = path.AsSpan(curr, i - curr).Trim();
                    curr = i + 1;

                    if (part.Equals("cache".AsSpan(), StringComparison.OrdinalIgnoreCase)
                        || part.Equals("svr".AsSpan(), StringComparison.OrdinalIgnoreCase)
                        || part.Equals("srv".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        // Don't yield this.
                    }
                    else if (part.Equals("symsrv".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        possiblySkipNextDll = true;
                    }
                    else
                    {
                        bool skip = possiblySkipNextDll && part.EndsWith(".dll".AsSpan(), StringComparison.OrdinalIgnoreCase);
                        possiblySkipNextDll = false;

                        if (!skip && part.Length > 0)
                            yield return part.ToString();
                    }
                }
            }

            if (curr < path.Length)
            {
                ReadOnlySpan<char> part = path.AsSpan(curr).Trim();
                bool skip = possiblySkipNextDll && part.EndsWith(".dll".AsSpan(), StringComparison.OrdinalIgnoreCase);
                if (!skip && part.Length > 0)
                    yield return part.ToString();
            }
        }

        private static bool IsUrl(string path)
        {
            bool result = Uri.TryCreate(path, UriKind.Absolute, out Uri? uriResult)
                          && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            return result;
        }
    }
}