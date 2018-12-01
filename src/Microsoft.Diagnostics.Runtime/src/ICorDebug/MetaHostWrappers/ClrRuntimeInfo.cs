// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    /// <summary>
    /// Wrapper for ICLRRuntimeInfo. Represents information about a CLR install instance.
    /// </summary>
    internal sealed class CLRRuntimeInfo
    {
        public CLRRuntimeInfo(object clrRuntimeInfo)
        {
            m_runtimeInfo = (ICLRRuntimeInfo)clrRuntimeInfo;
        }

        public string GetVersionString()
        {
            StringBuilder sb = new StringBuilder(CLRMetaHost.MaxVersionStringLength);
            int verStrLength = sb.Capacity;
            m_runtimeInfo.GetVersionString(sb, ref verStrLength);
            return sb.ToString();
        }

        public string GetRuntimeDirectory()
        {
            StringBuilder sb = new StringBuilder();
            int strLength = 0;
            m_runtimeInfo.GetRuntimeDirectory(sb, ref strLength);
            sb.Capacity = strLength;
            int ret = m_runtimeInfo.GetRuntimeDirectory(sb, ref strLength);
            if (ret < 0)
                Marshal.ThrowExceptionForHR(ret);
            return sb.ToString();
        }

        public ICorDebug GetLegacyICorDebugInterface()
        {
            Guid ifaceId = typeof(ICorDebug).GetGuid();
            Guid clsId = s_ClsIdClrDebuggingLegacy;
            return (ICorDebug)m_runtimeInfo.GetInterface(ref clsId, ref ifaceId);
        }

        private static readonly Guid s_ClsIdClrDebuggingLegacy = new Guid("DF8395B5-A4BA-450b-A77C-A9A47762C520");
        private static Guid s_ClsIdClrProfiler = new Guid("BD097ED8-733E-43FE-8ED7-A95FF9A8448C");
        private static Guid s_CorMetaDataDispenser = new Guid("E5CB7A31-7512-11d2-89CE-0080C792E5D8");

        private readonly ICLRRuntimeInfo m_runtimeInfo;
    }
}