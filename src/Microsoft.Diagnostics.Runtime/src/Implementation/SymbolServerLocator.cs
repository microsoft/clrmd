using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

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

        public string? FindPEImage(string fileName, string archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties)
        {
            foreach (IFileLocator locator in _groups)
            {
                string? result = locator.FindPEImage(fileName, archivedUnder, buildIdOrUUID, originalPlatform, checkProperties);
                if (result != null)
                    return result;
            }

            return null;
        }

        public string? FindElfImage(string fileName, string? archivedUnder, ImmutableArray<byte> buildId, bool checkProperties)
        {
            foreach (IFileLocator locator in _groups)
            {
                string? result = locator.FindElfImage(fileName, archivedUnder, buildId, checkProperties);
                if (result != null)
                    return result;
            }

            return null;
        }

        public string? FindMachOImage(string fileName, string? archivedUnder, ImmutableArray<byte> uuid, bool checkProperties)
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

        public static IFileLocator CreateFromSymbolPath(string symbolPath)
        {
            FileSymbolCache defaultCache = GetDefaultCache();
            List<IFileLocator> locators = new List<IFileLocator>();

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
                    if (server.StartsWith("http:", StringComparison.OrdinalIgnoreCase) || server.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
                    {
                        SymbolServer symSvr = new SymbolServer(cache, server);
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
                return new SymbolServer(defaultCache, SymbolServer.Msdl);

            return new SymbolGroup(locators);
        }

        private static (string? Cache, string[] Servers) EnumerateEntries(string part)
        {
            if (!part.Contains('*'))
                return (null, new string[] { part });

            string[] split = part.Split('*');
            DebugOnly.Assert(split.Length > 1);

            if (split[0].Equals("cache"))
                return (split[1], split.Skip(2).ToArray());


            if (split[0].Equals("symsrv", StringComparison.OrdinalIgnoreCase))
            {
                // We don't really support this, but we'll make it work...ish.
                // Convert symsrv*symstore.dll*DownStream*server -> srv*DownStream*server

                if (split.Length < 3)
                    return (null, new string[] { part });

                split = new string[] {"srv"}.Concat(split.Skip(2)).ToArray();                
            }


            if (split[0].Equals("svr", StringComparison.OrdinalIgnoreCase) || split[0].Equals("srv", StringComparison.OrdinalIgnoreCase))
            {
                string? cache = split[1];

                if (string.IsNullOrWhiteSpace(cache))
                    cache = null;

                // e.g. "svr*http://symbols.com/"
                if (split.Length == 2)
                {
                    if (cache is null)
                        return (split[1], split.Skip(2).ToArray());

                    return (null, new string[] { cache });
                }
            }

            // Ok, so we have * but it didn't start with srv or svr, so what now?
            return (null, split.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray());
        }
    }


    internal sealed class SymbolServer : FileLocatorBase
    {
        public const string Msdl = "https://msdl.microsoft.com/download/symbols";

        private readonly FileSymbolCache _cache;
        private readonly HttpClient _http = new HttpClient();

        public bool SupportsCompression { get; private set; } = true;
        public bool SupportsRedirection { get; private set; } = false;

        public string Server { get; private set; }

        internal SymbolServer(FileSymbolCache cache, string server)
        {
            if (cache is null)
                throw new ArgumentNullException(nameof(cache));

            _cache = cache;
            Server = server;

            if (IsSymweb(server))
            {
                SupportsCompression = false;
                SupportsRedirection = true;
            }
        }

        private bool IsSymweb(string server)
        {
            try
            {
                Uri uri = new Uri(server);
                return uri.Host.Equals("symweb", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("symweb.corp.microsoft.com", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }



        public override string? FindElfImage(string fileName, string? archivedUnder, ImmutableArray<byte> buildId, bool checkProperties)
        {
            string? result = _cache.FindElfImage(fileName, archivedUnder, buildId, checkProperties);
            if (result != null)
                return result;

            string? key = base.FindElfImage(fileName, archivedUnder, buildId, checkProperties);
            if (key == null)
                return null;

            Stream? stream = FindFileOnServer(key).Result;
            if (stream != null)
                return _cache.Store(stream, key);

            return null;
        }

        public override string? FindMachOImage(string fileName, string? archivedUnder, ImmutableArray<byte> uuid, bool checkProperties)
        {
            string? result = _cache.FindMachOImage(fileName, archivedUnder, uuid, checkProperties);
            if (result != null)
                return result;

            string? key = base.FindMachOImage(fileName, archivedUnder, uuid, checkProperties);
            if (key == null)
                return null;

            Stream? stream = FindFileOnServer(key).Result;
            if (stream != null)
                return _cache.Store(stream, key);

            return null;
        }

        public override string? FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            string? result = _cache.FindPEImage(fileName, buildTimeStamp, imageSize, checkProperties);
            if (result != null)
                return result;

            string? key = base.FindPEImage(fileName, buildTimeStamp, imageSize, checkProperties);
            if (key == null)
                return null;

            Stream? stream = FindFileOnServer(key).Result;
            if (stream != null)
                return _cache.Store(stream, key);

            return null;
        }

        public override string? FindPEImage(string fileName, string archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties)
        {
            string? result = _cache.FindPEImage(fileName, archivedUnder, buildIdOrUUID, originalPlatform, checkProperties);
            if (result != null)
                return result;

            string? key = base.FindPEImage(fileName, archivedUnder, buildIdOrUUID, originalPlatform, checkProperties);
            if (key == null)
                return null;

            Stream? stream = FindFileOnServer(key).Result;
            if (stream != null)
                return _cache.Store(stream, key);

            return null;
        }

        private async Task<Stream?> FindFileOnServer(string key)
        {
            string fullPath = $"{Server}/{key.Replace('\\', '/')}";

            // If this server supports redirected files (E.G. symweb), then the vast majority of files are
            // archived via redirection.  Check that first before trying others to reduce requests.

            Task<string?> redirectedFile = Task.FromResult<string?>(null);
            if (SupportsRedirection)
            {
                string filePtrPath = Path.Combine(Path.GetDirectoryName(fullPath)!, "file.ptr").Replace('\\', '/');
                redirectedFile = GetStringOrNull(filePtrPath);
            }

            string? path = await redirectedFile.ConfigureAwait(false);
            if (path is not null)
            {
                try
                {
                    if (path.StartsWith("PATH:"))
                    {
                        path = path.Substring(5);

                        if (File.Exists(path))
                            return File.OpenRead(path);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                }
            }

            Task<HttpResponseMessage> file = _http.GetAsync(fullPath);
            Task<HttpResponseMessage?> compressed = Task.FromResult<HttpResponseMessage?>(null);

            string compressedPath = fullPath.Substring(0, fullPath.Length - 1) + '_';
            if (SupportsCompression)
                compressed = _http.GetAsync(compressedPath)!;

            HttpResponseMessage fileResponse = await file.ConfigureAwait(false);
            if (fileResponse.IsSuccessStatusCode)
                return await fileResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
            else
                fileResponse.Dispose();

            HttpResponseMessage? compressedResponse = await compressed.ConfigureAwait(false);
            if (compressedResponse is not null && compressedResponse.IsSuccessStatusCode)
            {
                string tmpPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(compressedPath));
                string output = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(fullPath));

                Command.Run("Expand " + Command.Quote(tmpPath) + " " + Command.Quote(output));
                MemoryStream ms = new MemoryStream();
                using (var fs = File.OpenRead(output))
                    await fs.CopyToAsync(ms).ConfigureAwait(false);

                ms.Position = 0;
                try
                {
                    if (File.Exists(output))
                        File.Delete(output);

                    if (File.Exists(tmpPath))
                        File.Delete(tmpPath);
                }
                catch
                {
                }

                return ms;
            }

            return null;
        }

        private async Task<string?> GetStringOrNull(string filePtrPath)
        {
            try
            {
                return await _http.GetStringAsync(filePtrPath);
            }
            catch
            {
                return null;
            }
        }
    }
}
