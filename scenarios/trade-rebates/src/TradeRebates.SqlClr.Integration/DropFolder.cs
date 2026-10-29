using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Text;
using ContosoFoods.TradeRebates.Rules;
using Microsoft.SqlServer.Server;

namespace ContosoFoods.TradeRebates.SqlClr.Integration
{
    public static class DropFolder
    {
        private sealed class FileRow
        {
            public string Name = string.Empty;
            public string FullPath = string.Empty;
            public long Size;
            public DateTime LastWriteUtc;
            public bool Locked;
        }

        private sealed class ClaimRow
        {
            public int LineNumber;
            public string Kind = string.Empty;
            public string? DistributorCode;
            public int? Period;
            public string? InvoiceNo;
            public DateTime? InvoiceDate;
            public string? CustomerNo;
            public string? Gtin;
            public decimal? Quantity;
            public decimal? NetAmount;
            public string? Message;
        }

        /// <summary>rebate.clr_ListFiles(@folder, @pattern): files in a drop folder, oldest first.</summary>
        [SqlFunction(FillRowMethodName = nameof(FillFile), DataAccess = DataAccessKind.Read,
            TableDefinition = "FileName nvarchar(260), FullPath nvarchar(400), SizeBytes bigint, LastWriteUtc datetime2(0), IsLocked bit")]
        public static IEnumerable ListFiles(SqlString folder, SqlString pattern)
        {
            var rows = new List<FileRow>();
            if (folder.IsNull)
                return rows;

            string search = pattern.IsNull || pattern.Value.Length == 0 ? "*.*" : pattern.Value;
            using (CallerContext.Impersonate())
            {
                var dir = new DirectoryInfo(folder.Value);
                if (!dir.Exists)
                    throw new DirectoryNotFoundException("Drop folder " + folder.Value + " not found or not accessible by " + CallerContext.CallerName() + ".");

                foreach (FileInfo file in dir.GetFiles(search, SearchOption.TopDirectoryOnly))
                {
                    rows.Add(new FileRow
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        Size = file.Length,
                        LastWriteUtc = file.LastWriteTimeUtc,
                        Locked = IsLocked(file.FullName)
                    });
                }
            }

            rows.Sort((a, b) => a.LastWriteUtc.CompareTo(b.LastWriteUtc));
            return rows;
        }

        public static void FillFile(object row, out SqlString fileName, out SqlString fullPath, out SqlInt64 size, out DateTime? lastWriteUtc, out SqlBoolean isLocked)
        {
            var r = (FileRow)row;
            fileName = r.Name;
            fullPath = r.FullPath;
            size = r.Size;
            lastWriteUtc = r.LastWriteUtc;
            isLocked = r.Locked;
        }

        /// <summary>
        /// rebate.clr_ReadClaimFile(@path, @codePage): parsed sell-out file. One H row (header),
        /// one L row per accepted line and one E row per rejected line.
        /// </summary>
        [SqlFunction(FillRowMethodName = nameof(FillClaim), DataAccess = DataAccessKind.Read,
            TableDefinition = "LineNumber int, Kind nchar(1), DistributorCode nvarchar(20), Period int, InvoiceNo nvarchar(20), InvoiceDate date, " +
                              "CustomerNo nvarchar(20), Gtin nchar(14), Quantity decimal(19,4), NetAmount decimal(19,2), Message nvarchar(400)")]
        public static IEnumerable ReadClaimFile(SqlString path, SqlInt32 codePage)
        {
            var rows = new List<ClaimRow>();
            if (path.IsNull)
                return rows;

            Encoding encoding = codePage.IsNull || codePage.Value == 0 ? Encoding.GetEncoding(850) : Encoding.GetEncoding(codePage.Value);
            ClaimFile file;
            using (CallerContext.Impersonate())
            using (var stream = new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true))
            {
                file = ClaimFileParser.Parse(reader);
            }

            rows.Add(new ClaimRow
            {
                LineNumber = 0,
                Kind = "H",
                DistributorCode = file.DistributorCode,
                Period = file.Period == 0 ? (int?)null : file.Period,
                Quantity = file.TotalQuantity,
                NetAmount = file.TotalAmount,
                Message = file.Format
            });

            foreach (ClaimFileLine line in file.Lines)
            {
                rows.Add(new ClaimRow
                {
                    LineNumber = line.LineNumber,
                    Kind = "L",
                    InvoiceNo = line.InvoiceNo,
                    InvoiceDate = line.InvoiceDate,
                    CustomerNo = line.CustomerNo,
                    Gtin = line.Gtin,
                    Quantity = line.Quantity,
                    NetAmount = line.NetAmount
                });
            }

            foreach (ClaimFileError error in file.Errors)
                rows.Add(new ClaimRow { LineNumber = error.LineNumber, Kind = "E", Message = Truncate(error.Message, 400) });

            return rows;
        }

        public static void FillClaim(object row, out SqlInt32 lineNumber, out SqlString kind, out SqlString distributorCode, out SqlInt32 period,
            out SqlString invoiceNo, out DateTime? invoiceDate, out SqlString customerNo, out SqlString gtin,
            out SqlDecimal quantity, out SqlDecimal netAmount, out SqlString message)
        {
            var r = (ClaimRow)row;
            lineNumber = r.LineNumber;
            kind = r.Kind;
            distributorCode = r.DistributorCode ?? SqlString.Null;
            period = r.Period.HasValue ? new SqlInt32(r.Period.Value) : SqlInt32.Null;
            invoiceNo = r.InvoiceNo ?? SqlString.Null;
            invoiceDate = r.InvoiceDate;
            customerNo = r.CustomerNo ?? SqlString.Null;
            gtin = r.Gtin ?? SqlString.Null;
            quantity = r.Quantity.HasValue ? new SqlDecimal(r.Quantity.Value) : SqlDecimal.Null;
            netAmount = r.NetAmount.HasValue ? new SqlDecimal(r.NetAmount.Value) : SqlDecimal.Null;
            message = r.Message ?? SqlString.Null;
        }

        /// <summary>rebate.clr_MoveFile: moves a processed file to a dated sub folder, returns the new path.</summary>
        [SqlProcedure]
        public static void MoveFile(SqlString path, SqlString targetFolder, out SqlString newPath)
        {
            string target;
            using (CallerContext.Impersonate())
            {
                string folder = Path.Combine(targetFolder.Value, DateTime.Now.ToString("yyyyMM"));
                Directory.CreateDirectory(folder);
                target = Path.Combine(folder, Path.GetFileName(path.Value));
                if (File.Exists(target))
                    target = Path.Combine(folder, Path.GetFileNameWithoutExtension(path.Value) + "_" + DateTime.Now.ToString("HHmmssfff") + Path.GetExtension(path.Value));
                File.Move(path.Value, target);
            }
            newPath = target;
        }

        /// <summary>rebate.clr_WriteTextFile: writes a file via temp name + rename so readers never see partial content.</summary>
        [SqlProcedure]
        public static void WriteTextFile(SqlString path, [SqlFacet(MaxSize = -1)] SqlString content, SqlInt32 codePage)
        {
            Encoding encoding = codePage.IsNull || codePage.Value == 0 ? new UTF8Encoding(false) : Encoding.GetEncoding(codePage.Value);
            using (CallerContext.Impersonate())
            {
                string? folder = Path.GetDirectoryName(path.Value);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);
                string temp = path.Value + ".tmp";
                File.WriteAllText(temp, content.IsNull ? string.Empty : content.Value, encoding);
                if (File.Exists(path.Value))
                    File.Delete(path.Value);
                File.Move(temp, path.Value);
            }
        }

        private static bool IsLocked(string path)
        {
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
    }
}
