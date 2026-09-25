// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Security.AntiSSRF;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class SymbolServerTests : IDisposable
    {
        private readonly DirectoryInfo _cache = Directory.CreateTempSubdirectory("clrmd-symbols-");

        [Fact]
        public void PrivateSymbolServersAreDisabledByDefault()
        {
            Assert.False(new DataTargetOptions().AllowPrivateSymbolServers);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TransportChecksCertificateRevocation(bool allowPrivateSymbolServers)
        {
            using AntiSSRFHandler handler = SymbolServer.CreateHttpHandler(allowPrivateSymbolServers);
            Assert.Equal(X509RevocationMode.Online, handler.SslOptions.CertificateRevocationCheckMode);
            Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
            Assert.True(handler.AllowAutoRedirect);
        }

        [Theory]
        [InlineData("http://msdl.microsoft.com/download/symbols")]
        [InlineData("https://127.0.0.1/")]
        [InlineData("https://localhost/")]
        [InlineData("https://10.1.2.3/")]
        [InlineData("https://172.16.1.1/")]
        [InlineData("https://192.168.1.1/")]
        [InlineData("https://169.254.169.254/")]
        [InlineData("https://[::1]/")]
        [InlineData("https://[fd00::1]/")]
        public void DefaultLocatorBlocksInsecureEndpoints(string server)
        {
            DataTargetOptions options = new()
            {
                SymbolCachePath = _cache.FullName,
                SymbolPaths = [server]
            };

            AggregateException exception = Assert.Throws<AggregateException>(() =>
                options.FileLocator.FindPEImage("test.dll", 1, 2, false));

            Assert.IsType<AntiSSRFException>(Assert.Single(exception.Flatten().InnerExceptions));
            Assert.Empty(_cache.EnumerateFileSystemInfos());
        }

        [Fact]
        public void ParsedSymbolPathIsSecureByDefault()
        {
            IFileLocator locator = SymbolGroup.CreateFromSymbolPath($"srv*{_cache.FullName}*http://127.0.0.1/", false, null);

            AggregateException exception = Assert.Throws<AggregateException>(() => locator.FindPEImage("test.dll", 1, 2, false));

            Assert.IsType<AntiSSRFException>(Assert.Single(exception.Flatten().InnerExceptions));
            Assert.Empty(_cache.EnumerateFileSystemInfos());
        }

        [Theory]
        [InlineData(false, false, "Windows")]
        [InlineData(false, true, "Windows")]
        [InlineData(true, false, "Windows")]
        [InlineData(true, true, "Windows")]
        [InlineData(false, false, "Linux")]
        [InlineData(false, true, "Linux")]
        [InlineData(true, false, "Linux")]
        [InlineData(true, true, "Linux")]
        [InlineData(false, false, "OSX")]
        [InlineData(false, true, "OSX")]
        [InlineData(true, false, "OSX")]
        [InlineData(true, true, "OSX")]
        public async Task PrivateServerOptInDownloadsAndCaches(bool parsedSymbolPath, bool redirect, string platform)
        {
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
            Uri server = new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/symbols/");
            Task<List<string>> requests = ServeAsync(listener, redirect, timeout.Token);

            DataTargetOptions options = new()
            {
                SymbolCachePath = _cache.FullName,
                SymbolPaths = [server.AbsoluteUri],
                AllowPrivateSymbolServers = true
            };
            IFileLocator locator = parsedSymbolPath
                ? SymbolGroup.CreateFromSymbolPath($"srv*{_cache.FullName}*{server}", false, null, allowPrivateSymbolServers: true)
                : options.FileLocator;

            string? FindImage() => platform == "Windows"
                ? locator.FindPEImage("test.dll", 1, 2, false)
                : locator.FindPEImage("test.dll", SymbolProperties.Coreclr, ImmutableArray.Create<byte>(1, 2), OSPlatform.Create(platform), false);

            string? result = await Task.Run(FindImage).WaitAsync(timeout.Token);
            List<string> received = await requests;
            Assert.NotNull(result);
            Assert.Equal("symbol-data", File.ReadAllText(result!));
            string key = platform switch
            {
                "Windows" => "000000012",
                "Linux" => "elf-buildid-coreclr-0102",
                _ => "mach-uuid-coreclr-0102"
            };
            Assert.StartsWith($"GET /symbols/test.dll/{key}/test.dll HTTP/1.1\r\n", received[0]);
            Assert.Equal(redirect ? 2 : 1, received.Count);
            if (redirect)
                Assert.StartsWith("GET /redirected HTTP/1.1\r\n", received[1]);

            listener.Stop();
            Assert.Equal(result, FindImage());
        }

        [Fact]
        public async Task RedirectDoesNotForwardAuthorization()
        {
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
            Uri server = new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/symbols/");
            Task<List<string>> requests = ServeAsync(listener, redirect: true, timeout.Token);
            using HttpClient client = new(SymbolServer.CreateHttpHandler(allowPrivateSymbolServers: true));
            using HttpRequestMessage request = new(HttpMethod.Get, server);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

            using HttpResponseMessage response = await client.SendAsync(request, timeout.Token);
            List<string> received = await requests;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Authorization: Bearer test-token\r\n", received[0]);
            Assert.DoesNotContain("Authorization:", received[1]);
        }

        private static async Task<List<string>> ServeAsync(TcpListener listener, bool redirect, CancellationToken cancellationToken)
        {
            List<string> requests = [];
            int count = redirect ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                using TcpClient connection = await listener.AcceptTcpClientAsync(cancellationToken);
                using NetworkStream stream = connection.GetStream();
                using StreamReader reader = new(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                StringBuilder request = new();
                while (true)
                {
                    string? line = await reader.ReadLineAsync(cancellationToken);
                    if (line is null)
                        throw new EndOfStreamException("The symbol request ended before its headers were complete.");
                    if (line.Length == 0)
                        break;
                    request.Append(line).Append("\r\n");
                }

                requests.Add(request.ToString());
                string response = redirect && i == 0
                    ? "HTTP/1.1 302 Found\r\nLocation: /redirected\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    : "HTTP/1.1 200 OK\r\nContent-Length: 11\r\nConnection: close\r\n\r\nsymbol-data";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
            }

            return requests;
        }

        public void Dispose()
        {
            _cache.Delete(recursive: true);
        }
    }
}
