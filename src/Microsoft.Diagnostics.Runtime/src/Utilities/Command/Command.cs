// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Command represents a running of a command lineNumber process.  It is basically
    /// a wrapper over System.Diagnostics.Process, which hides the complexitity
    /// of System.Diagnostics.Process, and knows how to capture output and otherwise
    /// makes calling commands very easy.
    /// </summary>
    internal sealed class Command
    {
        /// <summary>
        /// The time the process started.
        /// </summary>
        public DateTime StartTime => Process.StartTime;

        /// <summary>
        /// returns true if the process has exited.
        /// </summary>
        public bool HasExited => Process.HasExited;

        /// <summary>
        /// The time the processed Exited.  (HasExited should be true before calling)
        /// </summary>
        public DateTime ExitTime => Process.ExitTime;

        /// <summary>
        /// The duration of the command (HasExited should be true before calling)
        /// </summary>
        public TimeSpan Duration => ExitTime - StartTime;

        /// <summary>
        /// The operating system ID for the subprocess.
        /// </summary>
        public int Id => Process.Id;

        /// <summary>
        /// The process exit code for the subprocess.  (HasExited should be true before calling)
        /// Often this does not need to be checked because Command.Run will throw an exception
        /// if it is not zero.   However it is useful if the CommandOptions.NoThrow property
        /// was set.
        /// </summary>
        public int ExitCode => Process.ExitCode;

        /// <summary>
        /// The standard output and standard error output from the command.  This
        /// is accumulated in real time so it can vary if the process is still running.
        /// This property is NOT available if the CommandOptions.OutputFile or CommandOptions.OutputStream
        /// is specified since the output is being redirected there.   If a large amount of output is
        /// expected (> 1Meg), the Run.AddOutputStream(Stream) is recommended for retrieving it since
        /// the large string is never materialized at one time.
        /// </summary>
        public string Output
        {
            get
            {
                if (_outputStream != null)
                    throw new Exception("Output not available if redirected to file or stream");

                return _output.ToString();
            }
        }

        /// <summary>
        /// Returns that CommandOptions structure that holds all the options that affect
        /// the running of the command (like Timeout, Input ...)
        /// </summary>
        public CommandOptions Options { get; }

        /// <summary>
        /// Run 'commandLine', sending the output to the console, and wait for the command to complete.
        /// This simulates what batch filedo when executing their commands.  It is a bit more verbose
        /// by default, however
        /// </summary>
        /// <param variable="commandLine">The command lineNumber to run as a subprocess</param>
        /// <param variable="options">Additional qualifiers that control how the process is run</param>
        /// <returns>A Command structure that can be queried to determine ExitCode, Output, etc.</returns>
        public static Command RunToConsole(string commandLine, CommandOptions options)
        {
            return Run(commandLine, options.Clone().AddOutputStream(Console.Out));
        }

        public static Command RunToConsole(string commandLine)
        {
            return RunToConsole(commandLine, new CommandOptions());
        }

        /// <summary>
        /// Run 'commandLine' as a subprocess and waits for the command to complete.
        /// Output is captured and placed in the 'Output' property of the returned Command
        /// structure.
        /// </summary>
        /// <param variable="commandLine">The command lineNumber to run as a subprocess</param>
        /// <param variable="options">Additional qualifiers that control how the process is run</param>
        /// <returns>A Command structure that can be queried to determine ExitCode, Output, etc.</returns>
        public static Command Run(string commandLine, CommandOptions options)
        {
            Command run = new Command(commandLine, options);
            run.Wait();
            return run;
        }

        public static Command Run(string commandLine)
        {
            return Run(commandLine, new CommandOptions());
        }

        /// <summary>
        /// Launch a new command and returns the Command object that can be used to monitor
        /// the restult.  It does not wait for the command to complete, however you
        /// can call 'Wait' to do that, or use the 'Run' or 'RunToConsole' methods. */
        /// </summary>
        /// <param variable="commandLine">The command lineNumber to run as a subprocess</param>
        /// <param variable="options">Additional qualifiers that control how the process is run</param>
        /// <returns>A Command structure that can be queried to determine ExitCode, Output, etc.</returns>
        public Command(string commandLine, CommandOptions options)
        {
            Options = options;
            _commandLine = commandLine;

            // See if the command is quoted and match it in that case
            Match m = Regex.Match(commandLine, "^\\s*\"(.*?)\"\\s*(.*)");
            if (!m.Success)
                m = Regex.Match(commandLine, @"\s*(\S*)\s*(.*)"); // thing before first space is command

            ProcessStartInfo startInfo = new ProcessStartInfo(m.Groups[1].Value, m.Groups[2].Value)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                ErrorDialog = false,
                CreateNoWindow = true,
                RedirectStandardInput = options.Input != null
            };

            Process = new Process {StartInfo = startInfo};
            Process.StartInfo = startInfo;
            _output = new StringBuilder();
            if (options.elevate)
            {
                options.useShellExecute = true;
                startInfo.Verb = "runas";
                if (options.currentDirectory == null)
                    options.currentDirectory = Environment.CurrentDirectory;
            }

            Process.OutputDataReceived += OnProcessOutput;
            Process.ErrorDataReceived += OnProcessOutput;

            if (options.environmentVariables != null)
            {
                // copy over the environment variables to the process startInfo options. 
                foreach (string key in options.environmentVariables.Keys)
                {
                    // look for %VAR% strings in the value and subtitute the appropriate environment variable. 
                    string value = options.environmentVariables[key];
                    if (value != null)
                    {
                        int startAt = 0;
                        for (;;)
                        {
                            m = new Regex(@"%(\w+)%").Match(value, startAt);
                            if (!m.Success) break;

                            string varName = m.Groups[1].Value;
                            string varValue;
                            if (startInfo.EnvironmentVariables.ContainsKey(varName))
                                varValue = startInfo.EnvironmentVariables[varName];
                            else
                            {
                                varValue = Environment.GetEnvironmentVariable(varName);
                                if (varValue == null)
                                    varValue = "";
                            }

                            // replace this instance of the variable with its definition.  
                            int varStart = m.Groups[1].Index - 1; // -1 becasue % chars are not in the group
                            int varEnd = varStart + m.Groups[1].Length + 2; // +2 because % chars are not in the group
                            value = value.Substring(0, varStart) + varValue + value.Substring(varEnd, value.Length - varEnd);
                            startAt = varStart + varValue.Length;
                        }
                    }

                    startInfo.EnvironmentVariables[key] = value;
                }
            }

            startInfo.WorkingDirectory = options.currentDirectory;

            _outputStream = options.outputStream;
            if (options.outputFile != null)
            {
                _outputStream = File.CreateText(options.outputFile);
            }

            try
            {
                Process.Start();
            }
            catch (Exception e)
            {
                string msg = "Failure starting Process\r\n" +
                    "    Exception: " + e.Message + "\r\n" +
                    "    Cmd: " + commandLine + "\r\n";

                if (Regex.IsMatch(startInfo.FileName, @"^(copy|dir|del|color|set|cd|cdir|md|mkdir|prompt|pushd|popd|start|assoc|ftype)", RegexOptions.IgnoreCase))
                    msg += "    Cmd " + startInfo.FileName + " implemented by Cmd.exe, fix by prefixing with 'cmd /c'.";
                throw new Exception(msg, e);
            }

            if (!startInfo.UseShellExecute)
            {
                // startInfo asyncronously collecting output
                Process.BeginOutputReadLine();
                Process.BeginErrorReadLine();
            }

            // Send any input to the command 
            if (options.input != null)
            {
                Process.StandardInput.Write(options.input);
                Process.StandardInput.Dispose();
            }
        }

        /// <summary>
        /// Create a subprocess to run 'commandLine' with no special options.
        /// <param variable="commandLine">The command lineNumber to run as a subprocess</param>
        /// </summary>
        public Command(string commandLine)
            : this(commandLine, new CommandOptions())
        {
        }

        /// <summary>
        /// Wait for a started process to complete (HasExited will be true on return)
        /// </summary>
        /// <returns>Wait returns that 'this' pointer.</returns>
        public Command Wait()
        {
            // we where told not to wait
            if (Options.noWait)
                return this;

            bool waitReturned = false;
            bool killed = false;
            try
            {
                Process.WaitForExit(Options.timeoutMSec);
                waitReturned = true;
                //  TODO : HACK we see to have a race in the async process stuff
                //  If you do Run("cmd /c set") you get truncated output at the
                //  Looks like the problem in the framework.  
                for (int i = 0; i < 10; i++)
                    Thread.Sleep(1);
            }
            finally
            {
                if (!Process.HasExited)
                {
                    killed = true;
                    Kill();
                }
            }

            // If we created the output stream, we should close it.  
            if (_outputStream != null && Options.outputFile != null)
                _outputStream.Dispose();
            _outputStream = null;

            if (waitReturned && killed)
                throw new Exception("Timeout of " + Options.timeoutMSec / 1000 + " sec exceeded\r\n    Cmd: " + _commandLine);

            if (Process.ExitCode != 0 && !Options.noThrow)
                ThrowCommandFailure(null);
            return this;
        }

        /// <summary>
        /// Throw a error if the command exited with a non-zero exit code
        /// printing useful diagnostic information along with the thrown message.
        /// This is useful when NoThrow is specified, and after post-processing
        /// you determine that the command really did fail, and an normal
        /// Command.Run failure was the appropriate action.
        /// </summary>
        /// <param name="message">An additional message to print in the throw (can be null)</param>
        public void ThrowCommandFailure(string message)
        {
            if (Process.ExitCode != 0)
            {
                string outSpec = "";
                if (_outputStream == null)
                {
                    string outStr = _output.ToString();
                    // Only show the first lineNumber the last two lines if there are a lot of output. 
                    Match m = Regex.Match(outStr, @"^(\s*\n)?(.+\n)(.|\n)*?(.+\n.*\S)\s*$");
                    if (m.Success)
                        outStr = m.Groups[2].Value + "    <<< Omitted output ... >>>\r\n" + m.Groups[4].Value;
                    else
                        outStr = outStr.Trim();
                    // Indent the output
                    outStr = outStr.Replace("\n", "\n    ");
                    outSpec = "\r\n  Output: {\r\n    " + outStr + "\r\n  }";
                }

                if (message == null)
                    message = "";
                else if (message.Length > 0)
                    message += "\r\n";
                throw new Exception(
                    message + "Process returned exit code 0x" + Process.ExitCode.ToString("x") + "\r\n" +
                    "  Cmd: " + _commandLine + outSpec);
            }
        }

        /// <summary>
        /// Get the underlying process object.  Generally not used.
        /// </summary>
        public Process Process { get; }

        /// <summary>
        /// Kill the process (and any child processses (recursively) associated with the
        /// running command).   Note that it may not be able to kill everything it should
        /// if the child-parent' chain is broken by a child that creates a subprocess and
        /// then dies itself.   This is reasonably uncommon, however.
        /// </summary>
        public void Kill()
        {
            // We use taskkill because it is built into windows, and knows
            // how to kill all subchildren of a process, which important. 
            // TODO (should we use WMI instead?)
            Console.WriteLine("Killing process tree " + Id + " Cmd: " + _commandLine);
            try
            {
                Run("taskkill /f /t /pid " + Process.Id);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            int ticks = 0;
            do
            {
                Thread.Sleep(10);
                ticks++;
                if (ticks > 100)
                {
                    Console.WriteLine("ERROR: process is not dead 1 sec after killing " + Process.Id);
                    Console.WriteLine("Cmd: " + _commandLine);
                }
            } while (!Process.HasExited);

            // If we created the output stream, we should close it.  
            if (_outputStream != null && Options.outputFile != null)
                _outputStream.Dispose();
            _outputStream = null;
        }

        /// <summary>
        /// Put double quotes around 'str' if necessary (handles quotes quotes.
        /// </summary>
        public static string Quote(string str)
        {
            if (str.IndexOf('"') < 0)
            {
                // Replace any " with \"  (and any \" with \\" and and \\" with \\\"  ...)
                str = Regex.Replace(str, "\\*\"", @"\$1");
            }

            return "\"" + str + "\"";
        }

        /// <summary>
        /// Given a string 'commandExe' look for it on the path the way cmd.exe would.
        /// Returns null if it was not found.
        /// </summary>
        public static string FindOnPath(string commandExe)
        {
            string ret = ProbeForExe(commandExe);
            if (ret != null)
                return ret;

            if (!commandExe.Contains("\\"))
            {
                foreach (string path in Paths)
                {
                    string baseExe = Path.Combine(path, commandExe);
                    ret = ProbeForExe(baseExe);
                    if (ret != null)
                        return ret;
                }
            }

            return null;
        }

        private static string ProbeForExe(string path)
        {
            if (File.Exists(path))
                return path;

            foreach (string ext in PathExts)
            {
                string name = path + ext;
                if (File.Exists(name))
                    return name;
            }

            return null;
        }

        private static string[] PathExts
        {
            get
            {
                if (s_pathExts == null)
                    s_pathExts = Environment.GetEnvironmentVariable("PATHEXT").Split(';');
                return s_pathExts;
            }
        }
        private static string[] s_pathExts;
        private static string[] Paths
        {
            get
            {
                if (s_paths == null)
                    s_paths = Environment.GetEnvironmentVariable("PATH").Split(';');
                return s_paths;
            }
        }
        private static string[] s_paths;

        /* called data comes to either StdErr or Stdout */
        private void OnProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if (_outputStream != null)
                _outputStream.WriteLine(e.Data);
            else
                _output.AppendLine(e.Data);
        }

        /* private state */
        private readonly string _commandLine;
        private readonly StringBuilder _output;
        private TextWriter _outputStream;
    }
}