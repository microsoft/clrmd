// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.Diagnostics.Runtime.Implementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    // Security regression tests for the symbol-server URI construction. The symbol
    // "key" is derived from dump-controlled module filenames, so it must not be able
    // to (a) traverse out of the server's base path or (b) redirect the request to a
    // different scheme/host/port (SSRF / credential-bearing request to an attacker).
    public class SymbolServerUriTests
    {
        private static readonly Uri Server = new("https://msdl.microsoft.com/download/symbols/");

        [Fact]
        public void TryCreateSymbolUri_BuildsSameHostUriUnderBasePath()
        {
            Uri? result = SymbolServer.TryCreateSymbolUri(Server, "coreclr.dll/0abcd1234ef0/coreclr.dll");

            Assert.NotNull(result);
            Assert.Equal(Server.Scheme, result!.Scheme);
            Assert.Equal(Server.Host, result.Host);
            Assert.Equal(Server.Port, result.Port);
            Assert.StartsWith("/download/symbols/", result.AbsolutePath);
            Assert.EndsWith("/coreclr.dll/0abcd1234ef0/coreclr.dll", result.AbsolutePath);
        }

        [Fact]
        public void TryCreateSymbolUri_NormalizesBackslashesToForwardSlashes()
        {
            Uri? result = SymbolServer.TryCreateSymbolUri(Server, @"coreclr.dll\0abcd1234ef0\coreclr.dll");

            Assert.NotNull(result);
            Assert.Equal(Server.Host, result!.Host);
            Assert.EndsWith("/coreclr.dll/0abcd1234ef0/coreclr.dll", result.AbsolutePath);
        }

        [Fact]
        public void TryCreateSymbolUri_TrimsLeadingSeparators()
        {
            Uri? result = SymbolServer.TryCreateSymbolUri(Server, "///coreclr.dll/0abcd1234ef0/coreclr.dll");

            Assert.NotNull(result);
            Assert.Equal(Server.Host, result!.Host);
            Assert.EndsWith("/download/symbols/coreclr.dll/0abcd1234ef0/coreclr.dll", result.AbsolutePath);
        }

        // Path-traversal keys must be rejected outright.
        [Theory]
        [InlineData("../../../etc/passwd")]
        [InlineData("coreclr.dll/../../../../secret")]
        [InlineData(@"..\..\windows\system32\x")]
        [InlineData("./coreclr.dll")]
        [InlineData("a/./b")]
        [InlineData("..")]
        [InlineData(".")]
        public void TryCreateSymbolUri_RejectsTraversalKeys(string key)
        {
            Assert.Null(SymbolServer.TryCreateSymbolUri(Server, key));
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("///")]
        [InlineData(@"\\")]
        public void TryCreateSymbolUri_RejectsEmptyOrSeparatorOnlyKeys(string key)
        {
            Assert.Null(SymbolServer.TryCreateSymbolUri(Server, key));
        }

        // Even for hostile keys that try to inject an absolute URL / alternate host,
        // the result must never point at a different scheme/host/port. Either the key
        // is escaped into a same-host relative path, or the call returns null.
        [Theory]
        [InlineData("http://attacker.example/evil.dll")]
        [InlineData("https://attacker.example/evil.dll")]
        [InlineData("//attacker.example/evil.dll")]
        [InlineData(@"\\attacker.example\evil.dll")]
        [InlineData("file:///etc/passwd")]
        [InlineData("coreclr.dll@attacker.example/evil.dll")]
        [InlineData("coreclr.dll?redirect=http://attacker.example")]
        [InlineData("coreclr.dll#http://attacker.example")]
        public void TryCreateSymbolUri_NeverEscapesToAnotherHost(string key)
        {
            Uri? result = SymbolServer.TryCreateSymbolUri(Server, key);

            if (result is not null)
            {
                Assert.Equal(Server.Scheme, result.Scheme);
                Assert.Equal(Server.Host, result.Host);
                Assert.Equal(Server.Port, result.Port);
                Assert.StartsWith("/download/symbols/", result.AbsolutePath);
            }
        }

        [Fact]
        public void TryCreateSymbolUri_EscapesSpecialCharactersInSegments()
        {
            // Spaces and other characters must be percent-encoded, not passed raw.
            Uri? result = SymbolServer.TryCreateSymbolUri(Server, "my file.dll/0abcd1234ef0/my file.dll");

            Assert.NotNull(result);
            Assert.Equal(Server.Host, result!.Host);
            Assert.DoesNotContain(" ", result.AbsoluteUri);
            Assert.Contains("my%20file.dll", result.AbsoluteUri);
        }

        [Fact]
        public void TryCreateSymbolUri_HonorsServerBasePathWithoutTrailingSlash()
        {
            Uri serverNoSlash = new("https://symweb.azurefd.net/download/symbols");
            Uri? result = SymbolServer.TryCreateSymbolUri(serverNoSlash, "coreclr.dll/0abcd1234ef0/coreclr.dll");

            Assert.NotNull(result);
            Assert.Equal(serverNoSlash.Host, result!.Host);
            Assert.EndsWith("/download/symbols/coreclr.dll/0abcd1234ef0/coreclr.dll", result.AbsolutePath);
        }
    }
}
