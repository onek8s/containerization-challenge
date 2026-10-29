using System;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.SqlServer.Server;

namespace ContosoFoods.TradeRebates.SqlClr.Integration
{
    /// <summary>
    /// Protects distributor bank account numbers at rest. Uses the machine key of the database server
    /// (CRYPTPROTECT_LOCAL_MACHINE) with an application entropy, so values can only be read back on this server.
    /// </summary>
    public static class BankDetails
    {
        private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;
        private const int CRYPTPROTECT_LOCAL_MACHINE = 0x4;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ContosoFoods.TradeRebates.BankDetails.v1");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr, ref DATA_BLOB pOptionalEntropy,
            IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr, ref DATA_BLOB pOptionalEntropy,
            IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        [SqlFunction(Name = "clr_ProtectBankAccount", IsDeterministic = false, DataAccess = DataAccessKind.None)]
        [return: SqlFacet(MaxSize = 1024)]
        public static SqlBinary Protect([SqlFacet(MaxSize = 64)] SqlString value)
        {
            if (value.IsNull)
                return SqlBinary.Null;
            byte[] plain = Encoding.UTF8.GetBytes(value.Value);
            return new SqlBinary(Transform(plain, protect: true));
        }

        [SqlFunction(Name = "clr_UnprotectBankAccount", IsDeterministic = false, DataAccess = DataAccessKind.None)]
        [return: SqlFacet(MaxSize = 64)]
        public static SqlString Unprotect([SqlFacet(MaxSize = 1024)] SqlBinary value)
        {
            if (value.IsNull)
                return SqlString.Null;
            byte[] plain = Transform(value.Value, protect: false);
            return new SqlString(Encoding.UTF8.GetString(plain));
        }

        private static byte[] Transform(byte[] input, bool protect)
        {
            var inBlob = new DATA_BLOB();
            var entropyBlob = new DATA_BLOB();
            var outBlob = new DATA_BLOB();
            GCHandle inHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
            GCHandle entropyHandle = GCHandle.Alloc(Entropy, GCHandleType.Pinned);
            try
            {
                inBlob.cbData = input.Length;
                inBlob.pbData = inHandle.AddrOfPinnedObject();
                entropyBlob.cbData = Entropy.Length;
                entropyBlob.pbData = entropyHandle.AddrOfPinnedObject();

                int flags = CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_LOCAL_MACHINE;
                bool ok = protect
                    ? CryptProtectData(ref inBlob, "TradeRebates", ref entropyBlob, IntPtr.Zero, IntPtr.Zero, flags, out outBlob)
                    : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out outBlob);
                if (!ok)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var result = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                return result;
            }
            finally
            {
                inHandle.Free();
                entropyHandle.Free();
                if (outBlob.pbData != IntPtr.Zero)
                    LocalFree(outBlob.pbData);
            }
        }
    }
}
