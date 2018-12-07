using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.Diagnostics.Runtime {
    public class ProcMapEntry
    {
        private static readonly Regex s_rxProcMaps = new Regex(
            @"^([0-9a-fA-F]+)-([0-9a-fA-F]+) ([a-zA-Z0-9_\-]{4,}) ([0-9a-fA-F]+) ([0-9a-fA-F]{2,}:[0-9a-fA-F]{2,}) (\d+)(?:[ \t]+([^\s].*?))?\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);

        public string Line { get; }
        public UIntPtr Start { get; }
        public UIntPtr End { get; }
        public UIntPtr Size => (UIntPtr)( End.ToUInt64() - Start.ToUInt64() );
        public string Perms { get; }
        public string Offset { get; }
        public string Device { get; }
        public string INode { get; }
        public string Path { get; }

        public ProcMapEntry(string line)
        {
            Line = line;
            
            var match = s_rxProcMaps.Match(line);
            if (!match.Success)
                throw new NotImplementedException("don't understand /proc/pid/map");

            Start = new UIntPtr(ulong.Parse(match.Groups[1].Value, NumberStyles.HexNumber));
            End = new UIntPtr(ulong.Parse(match.Groups[2].Value, NumberStyles.HexNumber));
            Perms = match.Groups[3].Value;
            Offset = match.Groups[4].Value;
            Device = match.Groups[5].Value;
            INode = match.Groups[6].Value;
            Path = match.Groups.Count == 8 ? match.Groups[7].Value : "";
        }

        public bool InRange(UIntPtr addr) =>
            InRange(addr.ToUInt64());
        
        public bool InRange(ulong addr) =>
            Start.ToUInt64() <= addr && End.ToUInt64() >= addr;

        public override string ToString() =>
            Line;

    }
}