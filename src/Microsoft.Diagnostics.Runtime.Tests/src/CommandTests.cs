// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class CommandTests
    {
        [WindowsFact]
        public void TestProperties()
        {
            Command command = Command.Run("cmd.exe /c");
            Process process = command.Process;
            Assert.True(process.HasExited);

            Assert.Equal(command.StartTime, process.StartTime);
            Assert.Equal(command.HasExited, process.HasExited);
            Assert.Equal(command.ExitTime, process.ExitTime);
            Assert.True(command.Duration > TimeSpan.Zero);
            Assert.Equal(command.Id, process.Id);
            Assert.Equal(command.ExitCode, process.ExitCode);
        }
    }

    public class CommandOptionsTests
    {
        [Fact]
        public void TestAddNoThrow()
        {
            CommandOptions options = new CommandOptions().AddNoThrow();
            Assert.True(options.NoThrow);
        }

        [Fact]
        public void TestAddStart()
        {
            CommandOptions options = new CommandOptions().AddStart();
            Assert.True(options.Start);
        }

        [Fact]
        public void TestAddUseShellExecute()
        {
            CommandOptions options = new CommandOptions().AddUseShellExecute();
            Assert.True(options.UseShellExecute);
        }

        [Fact]
        public void TestAddNoWindow()
        {
            CommandOptions options = new CommandOptions().AddNoWindow();
            Assert.True(options.NoWindow);
        }

        [Fact]
        public void TestAddNoWait()
        {
            CommandOptions options = new CommandOptions().AddNoWait();
            Assert.True(options.NoWait);
        }

        [Fact]
        public void TestAddElevate()
        {
            CommandOptions options = new CommandOptions().AddElevate();
            Assert.True(options.Elevate);
        }
    }
}
