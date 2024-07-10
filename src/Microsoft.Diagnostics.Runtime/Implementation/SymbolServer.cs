// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class SymbolServer : FileLocatorBase
    {
        public const string Msdl = "https://msdl.microsoft.com/download/symbols";
        private const string SymwebHost = "symweb.azurefd.net";
        private readonly TokenCredential? _tokenCredential;
        private AccessToken _accessToken;
        private readonly FileSymbolCache _cache;
        private readonly bool _trace;
        private readonly HttpClient _http = new();

        public string Server { get; private set; }

        internal SymbolServer(FileSymbolCache cache, string server, bool trace, TokenCredential? credential)
        {
            if (cache is null)
                throw new ArgumentNullException(nameof(cache));

            _cache = cache;
            _trace = trace;
            Server = server;

            if (IsSymweb(server))
            {
                _tokenCredential = credential ?? new DefaultAzureCredential();
            }
        }

        private static bool IsSymweb(string server)
        {
            try
            {
                Uri uri = new(server);
                return uri.Host.Equals(SymwebHost, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public override string? FindElfImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildId, bool checkProperties)
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

        public override string? FindMachOImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> uuid, bool checkProperties)
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

        public override string? FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties)
        {
            string? result = _cache.FindPEImage(fileName, archivedUnder, buildIdOrUUID, originalPlatform, checkProperties);
            if (result != null)
                return result;

            string? key = base.FindPEImage(fileName, archivedUnder, buildIdOrUUID, originalPlatform, checkProperties);
            if (key == null)
                return null;

            Task<Stream?> findFileTask = FindFileOnServer(key);
            try
            {
                Stream? stream = findFileTask.Result;
                if (stream != null)
                    return _cache.Store(stream, key);
            }
            catch (AggregateException)
            {
            }

            return null;
        }

        private async Task<Stream?> FindFileOnServer(string key)
        {
            string fullPath = $"{Server}/{key.Replace('\\', '/')}";

            string? accessToken = await GetAccessTokenAsync().ConfigureAwait(false);
            if (accessToken is not null)
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = await _http.GetAsync(fullPath).ConfigureAwait(false);

            if (_trace)
                Trace.WriteLine($"ClrMD symbol request: {fullPath} returned {response.StatusCode}");

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            response.Dispose();
            return null;
        }

        private async Task<string?> GetAccessTokenAsync()
        {
            if (_tokenCredential is null)
                return null;

            if (_accessToken.ExpiresOn <= DateTimeOffset.UtcNow.AddMinutes(2))
                _accessToken = await _tokenCredential.GetTokenAsync(new TokenRequestContext(["api://af9e1c69-e5e9-4331-8cc5-cdf93d57bafa/.default"]), default).ConfigureAwait(false);

            return _accessToken.Token;
        }
    }
}