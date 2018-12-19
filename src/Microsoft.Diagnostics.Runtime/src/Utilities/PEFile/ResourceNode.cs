// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [Obsolete]
    internal sealed unsafe class ResourceNode
    {
        public string Name { get; }
        public bool IsLeaf { get; }

        // If IsLeaf is true
        public int DataLength { get; }

        public byte* FetchData(int offsetInResourceData, int size, PEBuffer buff)
        {
            return buff.Fetch(_dataFileOffset + offsetInResourceData, size);
        }

        public FileVersionInfo GetFileVersionInfo()
        {
            PEBuffer buff = _file.AllocBuff();
            byte* bytes = FetchData(0, DataLength, buff);
            FileVersionInfo ret = new FileVersionInfo(bytes, DataLength);
            _file.FreeBuff(buff);
            return ret;
        }

        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            ToString(sw, "");
            return sw.ToString();
        }

        public static ResourceNode GetChild(ResourceNode node, string name)
        {
            if (node == null)
                return null;

            foreach (ResourceNode child in node.Children)
                if (child.Name == name)
                    return child;

            return null;
        }

        // If IsLeaf is false
        public List<ResourceNode> Children
        {
            get
            {
                if (_children == null && !IsLeaf)
                {
                    PEBuffer buff = _file.AllocBuff();
                    int resourceStartFileOffset = _file.Header.FileOffsetOfResources;

                    IMAGE_RESOURCE_DIRECTORY* resourceHeader = (IMAGE_RESOURCE_DIRECTORY*)buff.Fetch(
                        _nodeFileOffset,
                        sizeof(IMAGE_RESOURCE_DIRECTORY));

                    int totalCount = resourceHeader->NumberOfNamedEntries + resourceHeader->NumberOfIdEntries;
                    int totalSize = totalCount * sizeof(IMAGE_RESOURCE_DIRECTORY_ENTRY);

                    IMAGE_RESOURCE_DIRECTORY_ENTRY* entries = (IMAGE_RESOURCE_DIRECTORY_ENTRY*)buff.Fetch(
                        _nodeFileOffset + sizeof(IMAGE_RESOURCE_DIRECTORY),
                        totalSize);

                    PEBuffer nameBuff = _file.AllocBuff();
                    _children = new List<ResourceNode>();
                    for (int i = 0; i < totalCount; i++)
                    {
                        IMAGE_RESOURCE_DIRECTORY_ENTRY* entry = &entries[i];
                        string entryName = null;
                        if (_isTop)
                            entryName = IMAGE_RESOURCE_DIRECTORY_ENTRY.GetTypeNameForTypeId(entry->Id);
                        else
                            entryName = entry->GetName(nameBuff, resourceStartFileOffset);
                        Children.Add(new ResourceNode(entryName, resourceStartFileOffset + entry->DataOffset, _file, entry->IsLeaf));
                    }

                    _file.FreeBuff(nameBuff);
                    _file.FreeBuff(buff);
                }

                return _children;
            }
        }

        private void ToString(StringWriter sw, string indent)
        {
            sw.Write("{0}<ResourceNode", indent);
            sw.Write(" Name=\"{0}\"", Name);
            sw.Write(" IsLeaf=\"{0}\"", IsLeaf);

            if (IsLeaf)
            {
                sw.Write("DataLength=\"{0}\"", DataLength);
                sw.WriteLine("/>");
            }
            else
            {
                sw.Write("ChildCount=\"{0}\"", Children.Count);
                sw.WriteLine(">");
                foreach (ResourceNode child in Children)
                    child.ToString(sw, indent + "  ");
                sw.WriteLine("{0}</ResourceNode>", indent);
            }
        }

        internal ResourceNode(string name, int nodeFileOffset, PEFile file, bool isLeaf, bool isTop = false)
        {
            _file = file;
            _nodeFileOffset = nodeFileOffset;
            _isTop = isTop;
            IsLeaf = isLeaf;
            Name = name;

            if (isLeaf)
            {
                PEBuffer buff = _file.AllocBuff();
                IMAGE_RESOURCE_DATA_ENTRY* dataDescr = (IMAGE_RESOURCE_DATA_ENTRY*)buff.Fetch(nodeFileOffset, sizeof(IMAGE_RESOURCE_DATA_ENTRY));

                DataLength = dataDescr->Size;
                _dataFileOffset = file.Header.RvaToFileOffset(dataDescr->RvaToData);
                byte* data = FetchData(0, DataLength, buff);
                _file.FreeBuff(buff);
            }
        }

        private readonly PEFile _file;
        private readonly int _nodeFileOffset;
        private List<ResourceNode> _children;
        private readonly bool _isTop;
        private readonly int _dataFileOffset;
    }
}