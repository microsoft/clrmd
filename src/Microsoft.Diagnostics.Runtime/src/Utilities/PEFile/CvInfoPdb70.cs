using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal unsafe struct CV_INFO_PDB70
    {
        public const int PDB70CvSignature = 0x53445352; // RSDS in ascii

        public int CvSignature;
        public Guid Signature;
        public int Age;
        public fixed byte bytePdbFileName[1]; // Actually variable sized. 
        public string PdbFileName
        {
            get
            {
                fixed (byte* ptr = bytePdbFileName)
                    return System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)ptr);
            }
        }
    };
}