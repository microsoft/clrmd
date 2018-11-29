// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct IMAGEHLP_MODULE64
    {
        private const int MAX_PATH = 260;

        public UInt32 SizeOfStruct;
        public UInt64 BaseOfImage;
        public UInt32 ImageSize;
        public UInt32 TimeDateStamp;
        public UInt32 CheckSum;
        public UInt32 NumSyms;
        public DEBUG_SYMTYPE SymType;
        private fixed char _ModuleName[32];
        private fixed char _ImageName[256];
        private fixed char _LoadedImageName[256];
        private fixed char _LoadedPdbName[256];
        public UInt32 CVSig;
        public fixed char CVData[MAX_PATH * 3];
        public UInt32 PdbSig;
        public Guid PdbSig70;
        public UInt32 PdbAge;
        private UInt32 _bPdbUnmatched; /* BOOL */
        private UInt32 _bDbgUnmatched; /* BOOL */
        private UInt32 _bLineNumbers; /* BOOL */
        private UInt32 _bGlobalSymbols; /* BOOL */
        private UInt32 _bTypeInfo; /* BOOL */
        private UInt32 _bSourceIndexed; /* BOOL */
        private UInt32 _bPublics; /* BOOL */

        public bool PdbUnmatched
        {
            get { return _bPdbUnmatched != 0; }
            set { _bPdbUnmatched = value ? 1U : 0U; }
        }

        public bool DbgUnmatched
        {
            get { return _bDbgUnmatched != 0; }
            set { _bDbgUnmatched = value ? 1U : 0U; }
        }

        public bool LineNumbers
        {
            get { return _bLineNumbers != 0; }
            set { _bLineNumbers = value ? 1U : 0U; }
        }

        public bool GlobalSymbols
        {
            get { return _bGlobalSymbols != 0; }
            set { _bGlobalSymbols = value ? 1U : 0U; }
        }

        public bool TypeInfo
        {
            get { return _bTypeInfo != 0; }
            set { _bTypeInfo = value ? 1U : 0U; }
        }

        public bool SourceIndexed
        {
            get { return _bSourceIndexed != 0; }
            set { _bSourceIndexed = value ? 1U : 0U; }
        }

        public bool Publics
        {
            get { return _bPublics != 0; }
            set { _bPublics = value ? 1U : 0U; }
        }

        public string ModuleName
        {
            get
            {
                fixed (char* moduleNamePtr = _ModuleName)
                {
                    return Marshal.PtrToStringUni((IntPtr)moduleNamePtr, 32);
                }
            }
        }

        public string ImageName
        {
            get
            {
                fixed (char* imageNamePtr = _ImageName)
                {
                    return Marshal.PtrToStringUni((IntPtr)imageNamePtr, 256);
                }
            }
        }

        public string LoadedImageName
        {
            get
            {
                fixed (char* loadedImageNamePtr = _LoadedImageName)
                {
                    return Marshal.PtrToStringUni((IntPtr)loadedImageNamePtr, 256);
                }
            }
        }

        public string LoadedPdbName
        {
            get
            {
                fixed (char* loadedPdbNamePtr = _LoadedPdbName)
                {
                    return Marshal.PtrToStringUni((IntPtr)loadedPdbNamePtr, 256);
                }
            }
        }
    }
}
