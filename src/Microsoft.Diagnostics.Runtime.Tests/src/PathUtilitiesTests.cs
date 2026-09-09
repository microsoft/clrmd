// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.Implementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    // Security regression tests for CLRMD-001: dump-controlled UNC/device module
    // paths must never be treated as safe local paths (NTLM leak + DAC TOCTOU).
    // PathUtilities is the central gate every dump-controlled-path sink is gated on.
    public class PathUtilitiesTests
    {
        // Paths that begin with two directory separators are remote/device/redirector
        // paths and MUST be rejected on every platform, regardless of OS.
        [Theory]
        [InlineData(@"\\attacker.example\share\coreclr.dll")] // UNC
        [InlineData("//attacker.example/share/coreclr.dll")]  // forward-slash UNC
        [InlineData(@"\/attacker/share/coreclr.dll")]         // mixed separators
        [InlineData(@"/\attacker/share/coreclr.dll")]         // mixed separators
        [InlineData(@"\\?\C:\Windows\coreclr.dll")]           // extended-length local
        [InlineData(@"\\?\UNC\srv\share\coreclr.dll")]        // extended-length UNC
        [InlineData(@"\\.\PIPE\coreclr")]                     // device namespace
        [InlineData(@"\\;X:\\srv\share\coreclr.dll")]         // drive-substitution syntax
        [InlineData(@"\\srv@SSL\DavWWWRoot\coreclr.dll")]     // implicit WebDAV via UNC
        public void IsSafeAbsoluteLocalPath_RejectsRemoteOrDevicePaths_OnAllPlatforms(string path)
        {
            Assert.False(PathUtilities.IsSafeAbsoluteLocalPath(path));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void IsSafeAbsoluteLocalPath_RejectsNullOrWhitespace(string path)
        {
            Assert.False(PathUtilities.IsSafeAbsoluteLocalPath(path));
        }

        // Relative paths are not absolute local paths and must be rejected everywhere.
        [Theory]
        [InlineData("coreclr.dll")]
        [InlineData(@"sub\coreclr.dll")]
        [InlineData("sub/coreclr.dll")]
        [InlineData(@"..\coreclr.dll")]
        [InlineData("../coreclr.dll")]
        [InlineData(@".\coreclr.dll")]
        public void IsSafeAbsoluteLocalPath_RejectsRelativePaths_OnAllPlatforms(string path)
        {
            Assert.False(PathUtilities.IsSafeAbsoluteLocalPath(path));
        }

        [WindowsFact]
        public void IsSafeAbsoluteLocalPath_Windows_AcceptsDriveAbsolutePaths()
        {
            Assert.True(PathUtilities.IsSafeAbsoluteLocalPath(@"C:\Windows\coreclr.dll"));
            Assert.True(PathUtilities.IsSafeAbsoluteLocalPath("C:/Windows/coreclr.dll"));
            Assert.True(PathUtilities.IsSafeAbsoluteLocalPath(@"z:\a"));
            Assert.True(PathUtilities.IsSafeAbsoluteLocalPath(@"D:\"));
        }

        [WindowsFact]
        public void IsSafeAbsoluteLocalPath_Windows_RejectsMalformedOrNonDrivePaths()
        {
            // Drive letter without a following separator is not an absolute path.
            Assert.False(PathUtilities.IsSafeAbsoluteLocalPath(@"C:coreclr.dll"));
            Assert.False(PathUtilities.IsSafeAbsoluteLocalPath("C:"));
            // Non-letter "drive".
            Assert.False(PathUtilities.IsSafeAbsoluteLocalPath(@"1:\coreclr.dll"));
            // Rooted-but-not-drive path (still resolves against current drive -> unsafe).
            Assert.False(PathUtilities.IsSafeAbsoluteLocalPath(@"\coreclr.dll"));
            Assert.False(PathUtilities.IsSafeAbsoluteLocalPath("/coreclr.dll"));
        }

        [LinuxFact]
        public void IsSafeAbsoluteLocalPath_Unix_AcceptsRootedPaths()
        {
            Assert.True(PathUtilities.IsSafeAbsoluteLocalPath("/usr/share/dotnet/coreclr.dll"));
            Assert.True(PathUtilities.IsSafeAbsoluteLocalPath("/coreclr.dll"));
        }

        [LinuxFact]
        public void IsSafeAbsoluteLocalPath_Unix_RejectsWindowsDrivePaths()
        {
            // A Windows drive path is not a rooted Unix path.
            Assert.False(PathUtilities.IsSafeAbsoluteLocalPath(@"C:\Windows\coreclr.dll"));
        }

        // GetFileName must strip both separators on every platform (this is the
        // Path.GetFileName Linux-only-'/' bug that made CLRMD-002 possible).
        [Theory]
        [InlineData(@"\\attacker.example\share\coreclr.dll", "coreclr.dll")]
        [InlineData("//attacker.example/share/coreclr.dll", "coreclr.dll")]
        [InlineData(@"C:\Windows\coreclr.dll", "coreclr.dll")]
        [InlineData("/usr/share/dotnet/coreclr.dll", "coreclr.dll")]
        [InlineData(@"a\b/c.dll", "c.dll")]      // mixed separators
        [InlineData("a/b\\c.dll", "c.dll")]      // mixed separators
        [InlineData("coreclr.dll", "coreclr.dll")]
        [InlineData(@"dir\", "")]                 // trailing separator
        [InlineData("dir/", "")]
        public void GetFileName_StripsBothSeparators_OnAllPlatforms(string input, string expected)
        {
            Assert.Equal(expected, PathUtilities.GetFileName(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetFileName_PassesThroughNullOrEmpty(string input)
        {
            Assert.Equal(input, PathUtilities.GetFileName(input));
        }

        [Theory]
        [InlineData(@"\\srv\share\coreclr.dll")]
        [InlineData("//srv/share/coreclr.dll")]
        [InlineData(@"\\?\C:\x")]
        [InlineData(@"\\.\PIPE\x")]
        public void IsRemoteOrDevicePath_DetectsTwoSeparatorForms(string path)
        {
            Assert.True(PathUtilities.IsRemoteOrDevicePath(path));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("coreclr.dll")]
        [InlineData(@"C:\Windows\coreclr.dll")]
        [InlineData("/usr/share/coreclr.dll")]
        [InlineData(@"\single")]
        [InlineData("/single")]
        public void IsRemoteOrDevicePath_IsFalseForLocalOrRelativePaths(string path)
        {
            Assert.False(PathUtilities.IsRemoteOrDevicePath(path));
        }
    }
}
