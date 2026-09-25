// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Security.AntiSSRF;

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
        private readonly HttpClient _http;

        public Uri Server { get; private set; }
        private bool IsSymweb => Server.Host.Equals(SymwebHost.Host, StringComparison.OrdinalIgnoreCase);

        internal SymbolServer(FileSymbolCache cache, string server, bool trace, TokenCredential? credential, bool allowPrivateSymbolServers = false)
            : this(cache, Sanitize(server), trace, credential, allowPrivateSymbolServers)
        {
        }

        internal SymbolServer(FileSymbolCache cache, Uri server, bool trace, TokenCredential? credential, bool allowPrivateSymbolServers = false)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _trace = trace;
            Server = EnsureTrailingSlash(server);
            _tokenCredential = credential;

            if (IsSymweb)
                _tokenCredential ??= new InteractiveBrowserCredential();

            _http = new HttpClient(CreateHttpHandler(allowPrivateSymbolServers));
        }

        internal static AntiSSRFHandler CreateHttpHandler(bool allowPrivateSymbolServers)
        {
            AntiSSRFPolicy policy = new(allowPrivateSymbolServers ? PolicyConfigOptions.None : PolicyConfigOptions.ExternalOnlyLatest)
            {
                AllowPlainTextHttp = allowPrivateSymbolServers
            };

            AntiSSRFHandler handler = policy.GetHandler();
#if NET6_0_OR_GREATER
            handler.SslOptions.CertificateRevocationCheckMode = X509RevocationMode.Online;
#else
            // A netstandard consumer on .NET 8+ loads AntiSSRF's net8.0 asset, which
            // replaces CheckCertificateRevocationList with SslOptions. Select the loaded API.
            if (typeof(AntiSSRFHandler).GetProperty("CheckCertificateRevocationList") is { } revocationProperty)
            {
                revocationProperty.SetValue(handler, true);
            }
            else
            {
                object sslOptions = typeof(AntiSSRFHandler).GetProperty("SslOptions")?.GetValue(handler)
                    ?? throw new MissingMemberException(typeof(AntiSSRFHandler).FullName, "SslOptions");
                System.Reflection.PropertyInfo modeProperty = sslOptions.GetType().GetProperty("CertificateRevocationCheckMode")
                    ?? throw new MissingMemberException(sslOptions.GetType().FullName, "CertificateRevocationCheckMode");
                modeProperty.SetValue(sslOptions, X509RevocationMode.Online);
            }
#endif
            return handler;
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
            UriBuilder builder = new(uri)
            {
                Query = "",
                Fragment = ""
            };

            // If the URI's AbsolutePath already ends with '/', return as is.
            if (builder.Path.EndsWith("/"))
                return builder.Uri;

            // Rebuild the URI with a trailing slash in the path.
            builder.Path += "/";
            return builder.Uri;
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
            catch (AggregateException ex)
            {
                if (_trace)
                    Trace.WriteLine($"ClrMD symbol request for {key} failed: {ex}");
            }

            return null;
        }

        private async Task<Stream?> FindFileOnServer(string key)
        {
            Uri? fullPath = TryCreateSymbolUri(Server, key);
            if (fullPath is null)
                return null;

            string? accessToken = IsSymweb ? await GetAccessTokenAsync().ConfigureAwait(false) : null;
            using HttpRequestMessage request = new(HttpMethod.Get, fullPath);
            if (accessToken is not null && IsSameServer(Server, fullPath))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = await _http.SendAsync(request).ConfigureAwait(false);

            if (_trace)
                Trace.WriteLine($"ClrMD symbol request: {fullPath} returned {response.StatusCode}");

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            response.Dispose();
            return null;
        }

        internal static Uri? TryCreateSymbolUri(Uri server, string key)
        {
            string? relativePath = EscapeSymbolKey(key);
            if (relativePath is null)
                return null;

            Uri normalizedServer = EnsureTrailingSlash(server);
            string baseUri = normalizedServer.GetLeftPart(UriPartial.Path);
            if (!baseUri.EndsWith("/", StringComparison.Ordinal))
                baseUri += "/";

            Uri result = new(baseUri + relativePath, UriKind.Absolute);
            return IsSameServer(normalizedServer, result) ? result : null;
        }

        private static string? EscapeSymbolKey(string key)
        {
            string normalizedKey = key.Replace('\\', '/').TrimStart('/');
            if (normalizedKey.Length == 0)
                return null;

            string[] segments = normalizedKey.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return null;

            StringBuilder builder = new();
            foreach (string segment in segments)
            {
                if (segment == "." || segment == "..")
                    return null;

                if (builder.Length != 0)
                    builder.Append('/');

                builder.Append(Uri.EscapeDataString(segment));
            }

            return builder.ToString();
        }

        private static bool IsSameServer(Uri expected, Uri actual)
        {
            return expected.Scheme.Equals(actual.Scheme, StringComparison.OrdinalIgnoreCase)
                   && expected.Host.Equals(actual.Host, StringComparison.OrdinalIgnoreCase)
                   && expected.Port == actual.Port;
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
