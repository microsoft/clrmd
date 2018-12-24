using System;

namespace Microsoft.Diagnostics.Runtime.Tests.Fixtures
{
    public class ArrayConnection : ObjectConnection<ArrayConnection.ArraysHolder>
    {
        public ArrayConnection() : base(TestTargets.Arrays, typeof(ArraysHolder))
        {
        }

        /// <summary>
        /// Please keep definitions in sync with Array.cs
        /// </summary>
        public class ArraysHolder
        {
            public readonly int[] IntArray = new int[] { 0, 1, 2, 3, 4, 5 };

            public readonly string[] StringArray = new string[] { "first", "second", "third" };

            public readonly Guid[] GuidArray = new Guid[]
            {
            new Guid("{56C15C6D-FD5A-40CA-BB37-64CEEC6A9BD5}"),
            new Guid("{39C4902E-9960-4469-AEEF-E878E9C8218F}"),
            new Guid("{FF62DBCC-FEA8-4373-8014-09E97362911B}")
            };

            public readonly DateTime[] DateTimeArray = new DateTime[]

                {
                new DateTime(2018,12,24),
                new DateTime(1992, 1, 24),
                new DateTime(1991, 8, 31)
                };

            public readonly object[] ReferenceArrayWithBlanks = new object[] { new object(), null, new object() };

            public readonly SampleStruct[] StructArray = new SampleStruct[] { new SampleStruct(5, "Five"), new SampleStruct(10, "Ten") };
        }

        public struct SampleStruct
        {
            public readonly int Number;
            public readonly string ReferenceLoad;

            public SampleStruct(int num, string load)
            {
                Number = num;
                ReferenceLoad = load;
            }
        }
    }
}
