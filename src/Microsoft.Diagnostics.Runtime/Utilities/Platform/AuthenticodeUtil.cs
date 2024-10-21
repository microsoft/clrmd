using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Diagnostics.Runtime.Utilities;

public unsafe static class AuthenticodeUtil
{
    private const string Crypt32 = "crypt32.dll";
    private const string Wintrust = "wintrust.dll";

    private const int CERT_CHAIN_POLICY_MICROSOFT_ROOT = 7;
    private const int WTD_STATEACTION_CLOSE = 0x2;
    private const string DOTNET_DAC_CERT_OID = "1.3.6.1.4.1.311.84.4.1";

    /// <summary>
    /// Verifies the DAC signing and signature
    /// </summary>
    /// <param name="dacPath">file path to DAC file</param>
    /// <param name="fileLock">file stream keeping the DAC file locked until it is loaded</param>
    /// <returns>true valid, false not signed or invalid signature</returns>
    /// <exception cref="FileNotFoundException">DAC file path invalid or not found</exception>
    /// <exception cref="IOException"></exception>
    public static bool VerifyDacDll(string dacPath, out IDisposable? fileLock)
    {
        fileLock = null;

        string filePath = Path.GetFullPath(dacPath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(filePath);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Trace.TraceError("VerifyDacDll: not supported on Linux/MacOS");
            return false;
        }

        FileStream fs = File.OpenRead(filePath);
        fileLock = fs;

        WINTRUST_FILE_INFO trustInfo = new()
        {
            cbStruct = (uint)sizeof(WINTRUST_FILE_INFO),
            hFile = fs.SafeFileHandle.DangerousGetHandle(),
        };

        WINTRUST_DATA trustData = new()
        {
            cbStruct = (uint)sizeof(WINTRUST_DATA),
            dwUIChoice = 2,         // WTD_UI_NONE
            dwProvFlags = 0x1040,   // WTD_REVOCATION_CHECK_CHAIN | WTD_CACHE_ONLY_URL_RETRIEVAL
            dwStateAction = 1,      // WTD_STATEACTION_VERIFY
            dwUnionChoice = 1,      // WTD_CHOICE_FILE
            pFile = new IntPtr(&trustInfo)
        };

        Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new(0xaac56b, 0xcd44, 0x11d0, 0x8c, 0xc2, 0x0, 0xc0, 0x4f, 0xc2, 0x95, 0xee);
        int result = WinVerifyTrust(IntPtr.Zero, &WINTRUST_ACTION_GENERIC_VERIFY_V2, &trustData);

        try
        {
            if (result != 0)
            {
                Trace.TraceError($"VerifyDacDll: WinVerifyTrust failed {result:X} {filePath}");
                return false;
            }

            IntPtr provider = WTHelperProvDataFromStateData(trustData.hWVTStateData);
            if (provider == IntPtr.Zero)
            {
                Trace.TraceError($"VerifyDacDll: WTHelperProvDataFromStateData failed {filePath}");
                return false;
            }

            CRYPT_PROVIDER_SGNR* signer = WTHelperGetProvSignerFromChain(provider, 0, false, 0);
            if (signer == null)
            {
                Trace.TraceError($"VerifyDacDll: WTHelperGetProvSignerFromChain failed {filePath}");
                return false;
            }

            CERT_CHAIN_POLICY_PARA parameters = new()
            {
                cbSize = (uint)sizeof(CERT_CHAIN_POLICY_PARA)
            };

            CERT_CHAIN_POLICY_STATUS status = new()
            {
                cbSize = (uint)sizeof(CERT_CHAIN_POLICY_STATUS)
            };

            IntPtr policyOID = new(CERT_CHAIN_POLICY_MICROSOFT_ROOT);
            bool bTrusted = CertVerifyCertificateChainPolicy(policyOID, signer->pChainContext, &parameters, &status) && status.dwError == 0;
            if (!bTrusted)
            {
                Trace.TraceError($"VerifyDacDll: chain can't be verified for the specified policy or does not meet the policy: {status.dwError:X} {filePath}");
                return false;
            }

            CRYPT_PROVIDER_CERT* leafCert = WTHelperGetProvCertFromChain(signer, 0);
            if (leafCert == null)
            {
                Trace.TraceError($"VerifyDacDll: could not obtain the leaf most cert in signing chain {filePath}");
                return false;
            }

            using X509Certificate2 cert = new(leafCert->pCert);

            foreach (X509EnhancedKeyUsageExtension ekuExt in cert.Extensions.OfType<X509EnhancedKeyUsageExtension>())
            { 
                foreach (Oid oid in ekuExt.EnhancedKeyUsages)
                {
                    if (oid.Value == DOTNET_DAC_CERT_OID)
                    {
                        return true;
                    }
                }
            }

            Trace.TraceError($"VerifyDacDll: could not find DAC special OID EKU extension {filePath}");
            return false;
        }
        finally
        {
            trustData.dwStateAction = WTD_STATEACTION_CLOSE;
            result = WinVerifyTrust(IntPtr.Zero, &WINTRUST_ACTION_GENERIC_VERIFY_V2, &trustData);
        }
    }

    #region WinTrust

    [DllImport(Wintrust)]
    private static extern int WinVerifyTrust(IntPtr hwnd, Guid* pgActionID, WINTRUST_DATA* pWVTData);

    [DllImport(Wintrust)]
    private static extern IntPtr WTHelperProvDataFromStateData(IntPtr hStateData);

    [DllImport(Wintrust)]
    private static extern CRYPT_PROVIDER_SGNR* WTHelperGetProvSignerFromChain(IntPtr provider, uint idSigner, [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint idCounterSigner);

    [DllImport(Wintrust)]
    private static extern CRYPT_PROVIDER_CERT* WTHelperGetProvCertFromChain(CRYPT_PROVIDER_SGNR* pSgnr, uint idxCert);

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CRYPT_PROVIDER_SGNR
    {
        public uint cbStruct;
        public uint dwLowDateTime;
        public uint dwHighDateTime;
        public uint csCertChain;
        public IntPtr pasCertChain;
        public uint dwSignerType;
        public IntPtr psSigner;
        public uint dwError;
        public uint csCounterSigners;
        public IntPtr pasCounterSigners;
        public IntPtr pChainContext;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CRYPT_PROVIDER_CERT
    {
        public uint cbStruct;
        public IntPtr pCert;
        public readonly uint isCommercial; 
        public readonly uint isTrustedRoot;
        public readonly uint isSelfSigned;
        public readonly uint isTestCert;
        public readonly uint dwRevokedReason;
        public readonly uint dwConfidence;
        public readonly uint dwError;
        public readonly IntPtr pTrustListContext;
        public readonly uint isTrustListSignerCert;
        public readonly IntPtr pCtlContext;
        public readonly uint dwCtlError;
        public readonly uint isIsCyclic;
        public readonly IntPtr pChainElement;
    }

    #endregion

    #region Crypt32

    [DllImport(Crypt32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CertVerifyCertificateChainPolicy(
        IntPtr pszPolicyOID,
        IntPtr pChainContext,
        CERT_CHAIN_POLICY_PARA* pPolicyPara,
        CERT_CHAIN_POLICY_STATUS* pPolicyStatus);

    [StructLayout(LayoutKind.Sequential)]
    internal struct CERT_CHAIN_POLICY_STATUS
    {
        public uint cbSize;
        public uint dwError;
        public int lChainIndex;
        public int lElementIndex;
        public IntPtr pvExtraPolicyStatus;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CERT_CHAIN_POLICY_PARA
    {
        public uint cbSize;
        public uint dwFlags;
        public IntPtr pvExtraPolicyPara;
    }

    #endregion
}