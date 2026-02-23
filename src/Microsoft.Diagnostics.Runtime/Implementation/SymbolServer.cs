// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class SymbolServer : FileLocatorBase
    {
        public static readonly Uri Msdl = new("https://msdl.microsoft.com/download/symbols/");
        public static readonly Uri SymwebHost = new("https://symweb.azurefd.net/");
        private readonly TokenCredential? _tokenCredential;
        private AccessToken _accessToken;
        private readonly FileSymbolCache _cache;
        private readonly bool _trace;
        private readonly HttpClient _http = new();

        public Uri Server { get; private set; }
        private bool IsSymweb => Server.Host.Equals(SymwebHost.Host, StringComparison.OrdinalIgnoreCase);

        internal SymbolServer(FileSymbolCache cache, string server, bool trace, TokenCredential? credential)
            : this(cache, Sanitize(server), trace, credential)
        {
        }

        internal SymbolServer(FileSymbolCache cache, Uri server, bool trace, TokenCredential? credential)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _trace = trace;
            Server = EnsureTrailingSlash(server);
            _tokenCredential = credential;

            if (IsSymweb)
                _tokenCredential ??= new InteractiveBrowserCredential();
        }

        private static Uri Sanitize(string server)
        {
            UriBuilder builder = new(server) { Query = "" };
            return builder.Uri;
        }

        private static Uri EnsureTrailingSlash(Uri uri)
        {
            // We concatenate the Uri later, and if the last path does not end with a / then
            // it gets erased when we combine uris.
            // If the URI's AbsolutePath already ends with '/', return as is.
            if (uri.AbsolutePath.EndsWith("/"))
                return uri;

            // Rebuild the URI with a trailing slash in the path.
            var builder = new UriBuilder(uri)
            {
                Path = uri.AbsolutePath + "/"
            };
            return builder.Uri;
        }

        [Obsolete("Non-Windows DAC download is disabled. Provide the DAC path directly via ClrInfo.CreateRuntime(dacPath, ...).")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override string? FindElfImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildId, bool checkProperties)
#pragma warning restore CS0809
        {
            string? result = _cache.FindElfImage(fileName, archivedUnder, buildId, checkProperties);
            if (result != null)
                return result;

            string? key = base.FindElfImage(fileName, archivedUnder, buildId, checkProperties);
            throw new NotSupportedException(
                $"ClrMD does not download DAC binaries on non-Windows platforms because there is no " +
                $"mechanism to verify their integrity. Acquire the DAC through a mechanism you trust " +
                $"and provide the path directly via ClrInfo.CreateRuntime(dacPath, ...).\n\n" +
                $"The DAC you need is: {fileName}\n" +
                $"Expected build ID: {BuildIdToHex(buildId)}\n" +
                $"Symbol Key: {key}");
        }

        [Obsolete("Non-Windows DAC download is disabled. Provide the DAC path directly via ClrInfo.CreateRuntime(dacPath, ...).")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override string? FindMachOImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> uuid, bool checkProperties)
#pragma warning restore CS0809
        {
            string? result = _cache.FindMachOImage(fileName, archivedUnder, uuid, checkProperties);
            if (result != null)
                return result;

            string? key = base.FindMachOImage(fileName, archivedUnder, uuid, checkProperties);
            throw new NotSupportedException(
                $"ClrMD does not download DAC binaries on non-Windows platforms because there is no " +
                $"mechanism to verify their integrity. Acquire the DAC through a mechanism you trust " +
                $"and provide the path directly via ClrInfo.CreateRuntime(dacPath, ...).\n\n" +
                $"The DAC you need is: {fileName}\n" +
                $"Expected UUID: {BuildIdToHex(uuid)}\n" +
                $"Symbol Key: {key}");
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
            key = key.Replace('\\', '/').TrimStart('/');
            Uri fullPath = new(Server, key);

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

        private static string BuildIdToHex(ImmutableArray<byte> bytes)
        {
            if (bytes.IsDefaultOrEmpty)
                return "(none)";

            return string.Join("", bytes.Select(b => b.ToString("x2")));
        }
    }
}