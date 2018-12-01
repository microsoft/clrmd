// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    ///  Default Pack of 8 makes this struct 4 bytes too long
    ///  and so retrieving the last one will fail.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal sealed class MINIDUMP_MODULE
    {
        /// <summary>
        /// Address that module is loaded within target.
        /// </summary>
        private ulong _baseofimage;
        public ulong BaseOfImage => DumpNative.ZeroExtendAddress(_baseofimage);

        /// <summary>
        /// Size of image within memory copied from IMAGE_OPTIONAL_HEADER.SizeOfImage.
        /// Note that this is usually different than the file size.
        /// </summary>
        public uint SizeOfImage;

        /// <summary>
        /// Checksum, copied from IMAGE_OPTIONAL_HEADER.CheckSum. May be 0 if not optional
        /// header is not available.
        /// </summary>
        public uint CheckSum;

        /// <summary>
        /// TimeStamp in Unix 32-bit time_t format. Copied from IMAGE_FILE_HEADER.TimeDateStamp
        /// </summary>
        public uint TimeDateStamp;

        /// <summary>
        /// RVA within minidump of the string containing the full path of the module.
        /// </summary>
        public RVA ModuleNameRva;

        internal VS_FIXEDFILEINFO VersionInfo;

        private MINIDUMP_LOCATION_DESCRIPTOR _cvRecord;

        private MINIDUMP_LOCATION_DESCRIPTOR _miscRecord;

        private ulong _reserved0;
        private ulong _reserved1;

        /// <summary>
        /// Gets TimeDateStamp as a DateTime. This is based off a 32-bit value and will overflow in 2038.
        /// This is not the same as the timestamps on the file.
        /// </summary>
        public DateTime Timestamp
        {
            get
            {
                // TimeDateStamp is a unix time_t structure (32-bit value).
                // UNIX timestamps are in seconds since January 1, 1970 UTC. It is a 32-bit number
                // Win32 FileTimes represents the number of 100-nanosecond intervals since January 1, 1601 UTC.
                // We can create a System.DateTime from a FileTime.
                // 
                // See explanation here: http://blogs.msdn.com/oldnewthing/archive/2003/09/05/54806.aspx
                // and here http://support.microsoft.com/default.aspx?scid=KB;en-us;q167296
                long win32FileTime = 10000000 * (long)TimeDateStamp + 116444736000000000;
                return DateTime.FromFileTimeUtc(win32FileTime);
            }
        }
    }
}