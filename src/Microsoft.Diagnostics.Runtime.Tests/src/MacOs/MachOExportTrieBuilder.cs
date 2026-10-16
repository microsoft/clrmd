// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities.Platform;

namespace Microsoft.Diagnostics.Runtime.Tests.MacOs;

internal sealed class MachOExportTrieBuilder
{
    private readonly List<Node> _nodes = [];

    public Node Root { get; }

    public MachOExportTrieBuilder()
    {
        Root = CreateNode("root").AtOffset(0);
    }

    public MachOExportTrieBuilder AddExport(string name, MachOExport export, int? nodeOffset = null)
    {
        Node terminal = CreateNode(name).WithTerminal(export);
        if (nodeOffset is int offset)
        {
            terminal.AtOffset(offset);
        }

        Root.WithChild(name, terminal);
        return this;
    }

    public Node CreateNode(string? name = null)
    {
        var node = new Node(name ?? $"node{_nodes.Count}");
        _nodes.Add(node);
        return node;
    }

    public byte[] Build()
    {
        foreach (Node node in _nodes)
        {
            node.ResetChildOffsetSizes();
        }

        Dictionary<Node, int> sizes;
        Dictionary<Node, long> offsets;
        long trieLength;
        bool offsetSizeIncreased;
        do
        {
            sizes = _nodes.ToDictionary(node => node, node => node.GetSerializedSize());
            offsets = LayoutNodes(sizes);
            trieLength = GetTrieLength(offsets, sizes);

            // Child offsets are ULEB128 values, so increasing an offset can enlarge its node
            // and move later nodes. Encoded widths only increase and are bounded, guaranteeing
            // that this layout process terminates without an arbitrary iteration limit.
            offsetSizeIncreased = false;
            foreach (Node node in _nodes)
            {
                offsetSizeIncreased |= node.IncreaseChildOffsetSizes(offsets, trieLength);
            }
        }
        while (offsetSizeIncreased);

        ValidateLayout(offsets, sizes);
        byte[] result = new byte[checked((int)trieLength)];
        using var stream = new MemoryStream(result, writable: true);
        foreach (Node node in _nodes)
        {
            stream.Position = offsets[node];
            node.Write(stream, offsets, trieLength);
        }

        return result;
    }

    private Dictionary<Node, long> LayoutNodes(IReadOnlyDictionary<Node, int> sizes)
    {
        Dictionary<Node, long> offsets = new();
        long nextOffset = 0;
        foreach (Node node in _nodes)
        {
            long offset = node.ExplicitOffset ?? nextOffset;
            offsets.Add(node, offset);
            nextOffset = Math.Max(nextOffset, checked(offset + sizes[node]));
        }

        return offsets;
    }

    private static long GetTrieLength(
        IReadOnlyDictionary<Node, long> offsets,
        IReadOnlyDictionary<Node, int> sizes)
        => offsets.Max(pair => checked(pair.Value + sizes[pair.Key]));

    private static void ValidateLayout(
        IReadOnlyDictionary<Node, long> offsets,
        IReadOnlyDictionary<Node, int> sizes)
    {
        Node[] orderedNodes = [.. offsets.Keys.OrderBy(node => offsets[node])];
        long previousEnd = 0;
        foreach (Node node in orderedNodes)
        {
            long offset = offsets[node];
            if (offset < previousEnd)
            {
                throw new InvalidOperationException($"Node '{node.Name}' overlaps another export trie node.");
            }

            previousEnd = checked(offset + sizes[node]);
        }
    }

    internal sealed class Node
    {
        private readonly List<Child> _children = [];
        private byte[] _terminalData = [];
        private ulong? _declaredTerminalSize;

        internal string Name { get; }
        internal long? ExplicitOffset { get; private set; }

        internal Node(string name)
        {
            Name = name;
        }

        public Node AtOffset(int offset)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ExplicitOffset = offset;
            return this;
        }

        public Node WithTerminal(MachOExport export)
        {
            ArgumentNullException.ThrowIfNull(export);
            _terminalData = export.GetTerminalData();
            _declaredTerminalSize = null;
            return this;
        }

        public Node WithRawTerminal(byte[] data, ulong? declaredSize = null)
        {
            ArgumentNullException.ThrowIfNull(data);
            _terminalData = [.. data];
            _declaredTerminalSize = declaredSize;
            return this;
        }

        public Node WithChild(string label, Node target)
        {
            _children.Add(Child.ForNode(label, target));
            return this;
        }

        public Node WithChildAtEnd(string label)
        {
            _children.Add(Child.AtEnd(label));
            return this;
        }

        internal int GetSerializedSize()
        {
            if (_children.Count > byte.MaxValue)
            {
                throw new InvalidOperationException($"Node '{Name}' has more than 255 children.");
            }

            ulong terminalSize = _declaredTerminalSize ?? checked((ulong)_terminalData.Length);
            int size = GetUleb128Size(terminalSize) + _terminalData.Length + 1;
            foreach (Child child in _children)
            {
                byte[] label = child.GetLabelBytes();
                size = checked(size + label.Length + 1 + child.OffsetSize);
            }

            return size;
        }

        internal bool IncreaseChildOffsetSizes(IReadOnlyDictionary<Node, long> offsets, long trieLength)
        {
            bool increased = false;
            foreach (Child child in _children)
            {
                increased |= child.IncreaseOffsetSize(offsets, trieLength);
            }

            return increased;
        }

        internal void ResetChildOffsetSizes()
        {
            foreach (Child child in _children)
            {
                child.ResetOffsetSize();
            }
        }

        internal void Write(Stream stream, IReadOnlyDictionary<Node, long> offsets, long trieLength)
        {
            ulong terminalSize = _declaredTerminalSize ?? checked((ulong)_terminalData.Length);
            stream.WriteUleb128(terminalSize);
            stream.Write(_terminalData);
            stream.WriteByte(checked((byte)_children.Count));
            foreach (Child child in _children)
            {
                stream.Write(child.GetLabelBytes());
                stream.WriteByte(0);
                stream.WriteUleb128(child.GetOffset(offsets, trieLength));
            }
        }
    }

    private sealed class Child
    {
        private readonly string _label;
        private readonly Node? _target;
        private readonly bool _pointsAtEnd;

        public int OffsetSize { get; private set; } = 1;

        private Child(string label, Node? target, bool pointsAtEnd)
        {
            if (label.Contains('\0'))
            {
                throw new ArgumentException("An export trie edge cannot contain a null character.", nameof(label));
            }

            _label = label;
            _target = target;
            _pointsAtEnd = pointsAtEnd;
        }

        public static Child ForNode(string label, Node target) => new(label, target, pointsAtEnd: false);
        public static Child AtEnd(string label) => new(label, target: null, pointsAtEnd: true);

        public byte[] GetLabelBytes() => Encoding.UTF8.GetBytes(_label);

        public void ResetOffsetSize() => OffsetSize = 1;

        public bool IncreaseOffsetSize(IReadOnlyDictionary<Node, long> offsets, long trieLength)
        {
            int requiredSize = GetUleb128Size(GetOffset(offsets, trieLength));
            if (requiredSize <= OffsetSize)
            {
                return false;
            }

            OffsetSize = requiredSize;
            return true;
        }

        public ulong GetOffset(IReadOnlyDictionary<Node, long> offsets, long trieLength)
            => _pointsAtEnd ? checked((ulong)trieLength) : checked((ulong)offsets[_target!]);
    }

    private static int GetUleb128Size(ulong value)
    {
        int size = 0;
        do
        {
            size++;
            value >>= 7;
        }
        while (value != 0);

        return size;
    }
}