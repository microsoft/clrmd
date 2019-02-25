using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// An entry in the resource table.
    /// </summary>
    public class ResourceEntry
    {
        private static readonly ResourceEntry[] s_emptyChildren = new ResourceEntry[0];
        private ResourceEntry[] _children;
        private readonly int _offset;

        /// <summary>
        /// The PEImage containing this ResourceEntry.
        /// </summary>
        public PEImage Image { get; }

        /// <summary>
        /// The parent resource of this ResourceEntry.  Null if and only if this is the root node.
        /// </summary>
        public ResourceEntry Parent { get; }

        /// <summary>
        /// Resource Name.  May be null if this is the root node.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns true if this is a leaf, and contains data.
        /// </summary>
        public bool IsLeaf { get; }

        /// <summary>
        /// The number of children this entry contains.
        /// </summary>
        public int Count => Children.Count;

        /// <summary>
        /// Returns the i'th child.
        /// </summary>
        /// <param name="i">The child to return.</param>
        /// <returns>The i'th ResourceEntry child.</returns>
        public ResourceEntry this[int i] => Children[i];

        /// <summary>
        /// Returns the given resource child by name.
        /// </summary>
        /// <param name="name">The name of the child to return.</param>
        /// <returns>The child in question, or null if none are found with that name.</returns>
        public ResourceEntry this[string name] => Children.SingleOrDefault(c => c.Name == name);

        /// <summary>
        /// The children resources of this ResourceEntry.
        /// </summary>
        public IReadOnlyList<ResourceEntry> Children => GetChildren();

        internal ResourceEntry(PEImage image, ResourceEntry parent, string name, bool leaf, int offset)
        {
            Image = image;
            Parent = parent;
            Name = name;
            IsLeaf = leaf;
            _offset = offset;
        }

        /// <summary>
        /// The data associated with this entry.
        /// </summary>
        /// <returns>A byte array of the data, or a byte[] of length 0 if this entry contains no data.</returns>
        public byte[] GetData()
        {
            GetDataVaAndSize(out int va, out int size);
            if (size == 0 || va == 0)
                return new byte[0];

            byte[] result = new byte[size];
            int count = Image.Read(result, va, size);
            if (count < size)
                Array.Resize(ref result, count);

            return result;
        }

        /// <summary>
        /// A convenience function to get structured data out of this entry.
        /// </summary>
        /// <typeparam name="T">A struct type to convert.</typeparam>
        /// <param name="offset">The offset into the data.</param>
        /// <returns>The struct that was read out of the data section.</returns>
        public T GetData<T>(int offset = 0) where T : struct
        {
            byte[] data = GetData();
            int size = Marshal.SizeOf(typeof(T));
            if (size + offset > data.Length)
                throw new IndexOutOfRangeException();

            GCHandle hnd = GCHandle.Alloc(data, GCHandleType.Pinned);
            T result = (T)Marshal.PtrToStructure(hnd.AddrOfPinnedObject(), typeof(T));
            hnd.Free();

            return result;
        }

        private ResourceEntry[] GetChildren()
        {
            if (_children != null)
                return _children;

            if (IsLeaf)
                return _children = s_emptyChildren;

            ResourceEntry root = Image.Resources;
            int resourceStartFileOffset = root._offset;
            int offset = _offset;
            IMAGE_RESOURCE_DIRECTORY hdr = Image.Read<IMAGE_RESOURCE_DIRECTORY>(ref offset);

            int count = hdr.NumberOfNamedEntries + hdr.NumberOfIdEntries;
            ResourceEntry[] result = new ResourceEntry[count];

            for (int i = 0; i < count; i++)
            {
                IMAGE_RESOURCE_DIRECTORY_ENTRY entry = Image.Read<IMAGE_RESOURCE_DIRECTORY_ENTRY>(ref offset);
                string name;
                if (this == root)
                    name = IMAGE_RESOURCE_DIRECTORY_ENTRY.GetTypeNameForTypeId(entry.Id);
                else
                    name = GetName(ref entry, resourceStartFileOffset);

                result[i] = new ResourceEntry(Image, this, name, entry.IsLeaf, resourceStartFileOffset + entry.DataOffset);
            }

            return _children = result;
        }

        private string GetName(ref IMAGE_RESOURCE_DIRECTORY_ENTRY entry, int resourceStartFileOffset)
        {
            int offset = resourceStartFileOffset + entry.NameOffset;
            int len = Image.Read<ushort>(ref offset);

            StringBuilder sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
            {
                char c = (char)Image.Read<ushort>(ref offset);
                if (c == 0)
                    break;

                sb.Append(c);
            }

            return sb.ToString();
        }

        private void GetDataVaAndSize(out int va, out int size)
        {

            IMAGE_RESOURCE_DATA_ENTRY dataEntry = Image.Read<IMAGE_RESOURCE_DATA_ENTRY>(_offset);
            va = dataEntry.RvaToData;
            size = dataEntry.Size;
        }

        public override string ToString() => Name;
    }
}
