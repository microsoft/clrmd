// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Regression tests for issues #1368 and #1369: generic type parameter resolution
    /// for fields of generic types (Dictionary, LinkedList, etc.).
    /// </summary>
    public class GenericFieldTests : IClassFixture<GenericFieldTests.GenericConnection>
    {
        private readonly GenericConnection _connection;

        public GenericFieldTests(GenericConnection connection)
            => _connection = connection;

        /// <summary>
        /// Issue #1369: LinkedListNode.item field type should be the concrete type (string),
        /// not the unresolved generic parameter (T) or the canonical type (__Canon).
        /// </summary>
        [Fact]
        public void LinkedListNode_ItemField_HasConcreteType()
        {
            ClrHeap heap = _connection.Runtime.Heap;
            ClrObject node = heap.EnumerateObjects()
                .FirstOrDefault(o => o.Type?.Name?.Contains("LinkedListNode<System.String>") == true);

            Assert.True(node.IsValid, "Could not find LinkedListNode<string> on the heap.");

            ClrInstanceField itemField = node.Type!.GetFieldByName("item");
            Assert.NotNull(itemField);

            // The field type should be System.String, not T or System.__Canon
            Assert.NotNull(itemField.Type);
            Assert.Equal("System.String", itemField.Type!.Name);
        }

        /// <summary>
        /// Issue #1369: ReadObjectField for LinkedListNode.item should return the correct string value.
        /// </summary>
        [Fact]
        public void LinkedListNode_ReadObjectField_ReturnsString()
        {
            ClrHeap heap = _connection.Runtime.Heap;
            ClrObject node = heap.EnumerateObjects()
                .FirstOrDefault(o => o.Type?.Name?.Contains("LinkedListNode<System.String>") == true);

            Assert.True(node.IsValid, "Could not find LinkedListNode<string> on the heap.");

            ClrObject item = node.ReadObjectField("item");
            Assert.True(item.IsValid);
            Assert.True(item.Type!.IsString);

            string value = item.AsString()!;
            Assert.True(value == "alpha" || value == "beta", $"Unexpected linked list item value: {value}");
        }

        /// <summary>
        /// Issue #1368: Dictionary._entries field should be readable as a valid array.
        /// </summary>
        [Fact]
        public void Dictionary_EntriesField_IsReadableArray()
        {
            ClrHeap heap = _connection.Runtime.Heap;
            ClrObject dict = heap.EnumerateObjects()
                .FirstOrDefault(o => o.Type?.Name?.Contains("Dictionary<System.String, System.Int32>") == true);

            Assert.True(dict.IsValid, "Could not find Dictionary<string, int> on the heap.");

            ClrObject entries = dict.ReadObjectField("_entries");
            Assert.True(entries.IsValid, "ReadObjectField returned invalid object for _entries.");
            Assert.True(entries.Type!.IsArray, "entries should be an array type.");

            ClrArray arr = entries.AsArray();
            Assert.True(arr.Length > 0, "entries array should have elements.");
            Assert.Equal(1, arr.Rank);
        }

        /// <summary>
        /// Verifies that field types containing __Canon are not exposed to users.
        /// </summary>
        [Fact]
        public void Fields_ShouldNotExposeCanonTypes()
        {
            ClrHeap heap = _connection.Runtime.Heap;
            ClrObject node = heap.EnumerateObjects()
                .FirstOrDefault(o => o.Type?.Name?.Contains("LinkedListNode<System.String>") == true);

            Assert.True(node.IsValid);

            foreach (ClrInstanceField field in node.Type!.Fields)
            {
                if (field.Type?.Name != null)
                {
                    Assert.DoesNotContain("__Canon", field.Type.Name);
                }
            }
        }

        public class GenericConnection : Fixtures.ObjectConnection<GenericTypeCarrier>
        {
            public GenericConnection() : base(TestTargets.ClrObjects, typeof(GenericTypeCarrier).Name)
            {
            }
        }

        public class GenericTypeCarrier
        {
            public System.Collections.Generic.Dictionary<string, int> StringIntDictionary = new()
            {
                { "hello", 1 },
                { "world", 2 }
            };

            public System.Collections.Generic.LinkedList<string> StringLinkedList = CreateLinkedList();

            private static System.Collections.Generic.LinkedList<string> CreateLinkedList()
            {
                var list = new System.Collections.Generic.LinkedList<string>();
                list.AddLast("alpha");
                list.AddLast("beta");
                return list;
            }
        }
    }
}
