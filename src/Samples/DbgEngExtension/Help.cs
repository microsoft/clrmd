// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace DbgEngExtension
{
    public class Help : DbgEngCommand
    {
        internal const string Command = "help";
        private const string RootResource = "DbgEngExtension.Help.";
        private static readonly (string Variable, string Replacement)[] Replacements =
        {
            ("$FINDPOINTERSIN", FindPointersIn.Command),
            ("$NONPINNEDFLAG", FindPointersIn.ExpandNonPinnedObjectsFlag),
            ("$MADDRESS", MAddress.Command),
            ("$GCTONATIVE", GCToNative.Command),
            ("$GCNATIVE_ALL", GCToNative.All)
        };

        public Help(nint pUnknown, bool redirectConsoleOutput = true)
            : base(pUnknown, redirectConsoleOutput)
        {
        }

        public Help(IDisposable dbgeng, bool redirectConsoleOutput = false)
            : base(dbgeng, redirectConsoleOutput)
        {
        }

        public void Show(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                command = "help";

            string help = GetRawHelp(command);
            help = ReplaceVariables(help);
            Console.WriteLine(help);
        }

        private static string ReplaceVariables(string command)
        {
            int index = command.IndexOf('$');
            while (index > 0 && index < command.Length - 1)
            {
                ReadOnlySpan<char> span = command.AsSpan(index);

                foreach ((string name, string replacement) in Replacements)
                {
                    if (span.Length >= name.Length && name.AsSpan().SequenceEqual(span[..name.Length]))
                    {
                        command = command.Replace(name, replacement);
                        break;
                    }
                }

                index = command.IndexOf('$', index + 1);
            }

            return command;
        }

        private static string GetRawHelp(string command)
        {
            System.Reflection.Assembly assembly = typeof(Help).Assembly;
            string[] resources = assembly.GetManifestResourceNames();

            foreach (string file in resources.Where(f => f.StartsWith(RootResource)))
            {
                string helpFileCommand = Path.GetFileNameWithoutExtension(file[RootResource.Length..]);
                if (helpFileCommand.Equals(command, StringComparison.OrdinalIgnoreCase))
                {
                    Stream? stream = assembly.GetManifestResourceStream(file);
                    if (stream is null)
                        return $"Failed to read help stream {file}.";

                    return new StreamReader(stream).ReadToEnd();
                }
            }

            if (command != "help")
                return GetRawHelp("help");

            return $"No help found for command '{command}'.";
        }
    }
}