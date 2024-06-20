// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.Implementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Tests for SymbolGroup.EnumerateEntries
    /// </summary>
    public class SymbolGroupTests
    {
        [Fact]
        public void TestSymsrv()
        {
            const string sympathWithDll = "symsrv*symstore.dll*https://msdl.microsoft.com/download/symbols";
            (string cache, string[] servers) = SymbolGroup.EnumerateEntries(sympathWithDll);
            Assert.Null(cache);
            Assert.Equal(["https://msdl.microsoft.com/download/symbols"], servers);

            const string sympathWithoutDll = "symsrv*https://msdl.microsoft.com/download/symbols";
            (cache, servers) = SymbolGroup.EnumerateEntries(sympathWithoutDll);
            Assert.Null(cache);
            Assert.Equal(["https://msdl.microsoft.com/download/symbols"], servers);
        }

        [Fact]
        public void TestServerWithCache()
        {
            const string sympath = "srv*d:\\cache*https://msdl.microsoft.com/download/symbols";
            (string cache, string[] servers) = SymbolGroup.EnumerateEntries(sympath);
            Assert.Equal("d:\\cache", cache);
            Assert.Equal(["https://msdl.microsoft.com/download/symbols"], servers);

            const string sympathWithDll = "srv*d:\\cache*https://msdl.microsoft.com/download/symbols*https://msdl.microsoft.com/download/symbols2";
            (cache, servers) = SymbolGroup.EnumerateEntries(sympathWithDll);
            Assert.Equal("d:\\cache", cache);
            Assert.Equal(["https://msdl.microsoft.com/download/symbols", "https://msdl.microsoft.com/download/symbols2"], servers);

            const string sympathWithoutCache = "srv*https://msdl.microsoft.com/download/symbols*https://msdl.microsoft.com/download/symbols2";
            (cache, servers) = SymbolGroup.EnumerateEntries(sympathWithoutCache);
            Assert.Null(cache);
            Assert.Equal(["https://msdl.microsoft.com/download/symbols", "https://msdl.microsoft.com/download/symbols2"], servers);
        }

        [Fact]
        public void TestCache()
        {
            const string sympath = "cache*d:\\cache";
            (string cache, string[] servers) = SymbolGroup.EnumerateEntries(sympath);
            Assert.Equal("d:\\cache", cache);
            Assert.Empty(servers);

            const string sympathWithDll = "cache*d:\\cache*https://msdl.microsoft.com/download/symbols";
            (cache, servers) = SymbolGroup.EnumerateEntries(sympathWithDll);
            Assert.Equal("d:\\cache", cache);
            Assert.Equal(["https://msdl.microsoft.com/download/symbols"], servers);

            const string sympathWithDll2 = "cache*d:\\cache*https://msdl.microsoft.com/download/symbols*https://msdl.microsoft.com/download/symbols2";
            (cache, servers) = SymbolGroup.EnumerateEntries(sympathWithDll2);
            Assert.Equal("d:\\cache", cache);
            Assert.Equal(["https://msdl.microsoft.com/download/symbols", "https://msdl.microsoft.com/download/symbols2"], servers);
        }
    }
}
