// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime.Implementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class FileLocatorTests
    {
        public static readonly string WellKnownDac = "mscordacwks_X86_X86_4.6.96.00.dll";
        public static readonly int WellKnownDacTimeStamp = 0x55b96946;
        public static readonly int WellKnownDacImageSize = 0x006a8000;

        public static readonly string WellKnownNativePdb = "clr.pdb";
        public static readonly Guid WellKnownNativePdbGuid = new("0350aa66-2d49-4425-ab28-9b43a749638d");
        public static readonly int WellKnownNativePdbAge = 2;

        internal static IFileLocator GetLocator()
        {
            return SymbolGroup.CreateFromSymbolPath($"srv*{Helpers.TestWorkingDirectory}*https://msdl.microsoft.com/download/symbols", true, null);
        }

        [Fact]
        public void DefaultSymbolCacheIsSharedAcrossConcurrentLocators()
        {
            FieldInfo field = typeof(SymbolGroup).GetField("s_cache", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            object original = field.GetValue(null);
            field.SetValue(null, null);

            try
            {
                FileSymbolCache[] caches = new FileSymbolCache[32];
                Parallel.For(0, caches.Length, index => caches[index] = SymbolGroup.GetDefaultCache());
                Assert.All(caches, cache => Assert.Same(caches[0], cache));
            }
            finally
            {
                field.SetValue(null, original);
            }
        }

        [Fact]
        public void DefaultSymbolCacheIsPrivate()
        {
            using CacheDirectoryScope scope = new();
            string path = scope.CacheDirectory.GetPath();
            Assert.Equal(scope.CachePath, path);
            Assert.True(Directory.Exists(path));
            AssertPrivateDirectory(path);
            AssertPrivateDirectory(Path.GetDirectoryName(path)!);
        }

        [Fact]
        public void DefaultSymbolCacheIsRecreatedWhenMissing()
        {
            using CacheDirectoryScope scope = new();
            string path = scope.CacheDirectory.GetPath();
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);

            string recreated = scope.CacheDirectory.GetPath();
            Assert.True(Directory.Exists(recreated));
            Assert.Equal(path, recreated);
            AssertPrivateDirectory(recreated);
            AssertPrivateDirectory(Path.GetDirectoryName(recreated)!);
        }

        [Fact]
        public void ExistingDefaultSymbolCacheIsPrivate()
        {
            using CacheDirectoryScope scope = new();
            Directory.CreateDirectory(scope.CachePath);
            SetSharedPermissions(scope.CachePath);
            SetSharedPermissions(Path.GetDirectoryName(scope.CachePath)!);

            string path = scope.CacheDirectory.GetPath();
            AssertPrivateDirectory(path);
            AssertPrivateDirectory(Path.GetDirectoryName(path)!);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void DefaultSymbolCacheReestablishesDirectoryBeforeUse(bool removeDirectory, bool store)
        {
            using CacheDirectoryScope scope = new();
            string path = scope.CacheDirectory.GetPath();
            FileSymbolCache cache = new(scope.CacheDirectory);
            string existingFile = Path.Combine(path, "probe.dll");
            File.WriteAllText(existingFile, "cache contents");

            if (removeDirectory)
            {
                Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
            }
            else
            {
                SetSharedPermissions(path);
                SetSharedPermissions(Path.GetDirectoryName(path)!);
            }

            if (store)
            {
                using MemoryStream contents = new(new byte[] { 1, 2, 3 });
                string file = cache.Store(contents, "probe/key/file");
                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(file));
            }
            else
            {
                string found = cache.FindPEImage("probe.dll", SymbolProperties.Self, ImmutableArray.Create<byte>(1, 2, 3), OSPlatform.Linux, checkProperties: false);
                if (removeDirectory)
                    Assert.Null(found);
                else
                {
                    Assert.Equal(existingFile, found);
                    Assert.Equal("cache contents", File.ReadAllText(found));
                }
            }

            Assert.True(Directory.Exists(path));
            AssertPrivateDirectory(path);
            AssertPrivateDirectory(Path.GetDirectoryName(path)!);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DefaultSymbolCacheRestoresPreviouslyStoredFilesWhenMissing(bool removeDirectory)
        {
            using CacheDirectoryScope scope = new();
            FileSymbolCache cache = new(scope.CacheDirectory);
            byte[] bytes = [1, 2, 3];
            using MemoryStream original = new(bytes);
            string path = cache.Store(original, "probe/key/file");

            if (removeDirectory)
                Directory.Delete(scope.CachePath, recursive: true);
            else
                File.Delete(path);

            using MemoryStream replacement = new(bytes);
            Assert.Equal(path, cache.Store(replacement, "probe/key/file"));
            Assert.True(File.Exists(path));
            Assert.Equal(bytes, File.ReadAllBytes(path));
            AssertPrivateDirectory(scope.CachePath);
        }

        [Fact]
        public void SymbolCachePreservesPreviouslyStoredFiles()
        {
            using CacheDirectoryScope scope = new();
            FileSymbolCache cache = new(scope.CacheDirectory);
            byte[] bytes = [1, 2, 3];
            using MemoryStream original = new(bytes);
            string path = cache.Store(original, "probe/key/file");

            using MemoryStream replacement = new(new byte[] { 4, 5, 6 });
            Assert.Equal(path, cache.Store(replacement, "probe/key/file"));
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        [Fact]
        public void DefaultSymbolCacheDoesNotChangeExistingDataDirectoryPermissions()
        {
            using CacheDirectoryScope scope = new();
            Directory.CreateDirectory(scope.LocalApplicationData);
            UnixFileMode mode = UnixFileMode.None;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                mode = File.GetUnixFileMode(scope.LocalApplicationData);

            scope.CacheDirectory.GetPath();

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.Equal(mode, File.GetUnixFileMode(scope.LocalApplicationData));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void DefaultSymbolCacheRequiresDataDirectory(string root)
        {
            Assert.Throws<InvalidOperationException>(() => new SymbolCacheDirectory(root).GetPath());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DefaultSymbolCacheRequiresNonSymbolicDirectories(bool linkParent)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            using CacheDirectoryScope scope = new();
            string path = scope.CacheDirectory.GetPath();
            string link = linkParent ? Path.GetDirectoryName(path)! : path;
            Directory.Delete(link, recursive: true);
            string destination = Path.Combine(scope.Root, "destination");
            Directory.CreateDirectory(destination);
            Directory.CreateSymbolicLink(link, destination);

            Assert.Throws<IOException>(() => scope.CacheDirectory.GetPath());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DefaultSymbolCacheRequiresDirectories(bool replaceParent)
        {
            using CacheDirectoryScope scope = new();
            string path = scope.CacheDirectory.GetPath();
            string file = replaceParent ? Path.GetDirectoryName(path)! : path;
            Directory.Delete(file, recursive: true);
            File.WriteAllText(file, "contents");

            Assert.ThrowsAny<IOException>(() => scope.CacheDirectory.GetPath());
            Assert.Equal("contents", File.ReadAllText(file));
        }

        [Fact]
        public void SymbolCacheRequiresLocation()
        {
            Assert.Throws<ArgumentNullException>(() => new FileSymbolCache(cacheLocation: null!));
            Assert.Throws<ArgumentNullException>(() => SymbolCacheDirectory.EnsureExists(null!));
        }

        [Fact]
        public void ExplicitSymbolCacheIsPreserved()
        {
            using CacheDirectoryScope scope = new();
            string path = Path.Combine(scope.Root, "explicit");
            Directory.CreateDirectory(path);
            SetSharedPermissions(path);
            DataTargetOptions options = new()
            {
                SymbolCachePath = path,
                SymbolPaths = []
            };

            Assert.Equal(path, options.SymbolCachePath);
            Assert.NotNull(options.FileLocator);
            FileSymbolCache cache = new(path);
            using MemoryStream contents = new(new byte[] { 1 });
            cache.Store(contents, "probe/key/file");
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.Equal((UnixFileMode)0x1ff, File.GetUnixFileMode(path));
        }

        private static void SetSharedPermissions(string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                File.SetUnixFileMode(path, (UnixFileMode)0x1ff);
        }

        private static void AssertPrivateDirectory(string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(path));
        }

        private sealed class CacheDirectoryScope : IDisposable
        {
            public CacheDirectoryScope()
            {
                CacheDirectory = new(LocalApplicationData);
            }

            public string Root { get; } = Directory.CreateTempSubdirectory("clrmd-symbol-cache-tests-").FullName;
            public string LocalApplicationData => Path.Combine(Root, "profile", "data");
            public SymbolCacheDirectory CacheDirectory { get; }
            public string CachePath => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(LocalApplicationData, "Microsoft", "ClrMD", "symbols")
                : Path.Combine(LocalApplicationData, "Microsoft.ClrMD", "symbols");

            public void Dispose() => Directory.Delete(Root, recursive: true);
        }

        [Fact(Skip = "Touches network, don't run regularly.")]
        public void FindBinaryNegativeTest()
        {
            IFileLocator locator = GetLocator();
            string dac = locator.FindPEImage(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false);
            Assert.Null(dac);
        }


        [Fact(Skip = "Touches network, don't run regularly.")]
        public void FindBinaryTest()
        {
            IFileLocator locator = GetLocator();
            string dac = locator.FindPEImage(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
            Assert.NotNull(dac);
            Assert.True(File.Exists(dac));
        }
    }
}
