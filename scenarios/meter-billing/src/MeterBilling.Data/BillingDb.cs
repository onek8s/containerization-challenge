using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Coho.Billing.Core;
using Coho.Billing.Core.Rating;
using Coho.Billing.Core.Runs;

namespace Coho.Billing.Data
{
    /// <summary>Data access for the MeterBilling database (schema bill).</summary>
    public class BillingDb
    {
        private readonly string _connectionString;

        public BillingDb(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static BillingDb FromConfig()
        {
            var setting = ConfigurationManager.ConnectionStrings["MeterBilling"];
            if (setting == null)
            {
                throw new ConfigurationErrorsException("Connection string 'MeterBilling' is missing");
            }

            return new BillingDb(setting.ConnectionString);
        }

        public SqlConnection Open()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        // ---- cycles and runs ----

        public List<BillCycle> GetDueCycles(DateTime today)
        {
            const string sql = @"
SELECT c.CycleCode, c.Name, c.IntervalMonths, c.NextCutOff, c.ReadWindowDays, c.PaymentDays
FROM bill.BillCycle c
WHERE c.Active = 1 AND c.NextCutOff <= @today
  AND NOT EXISTS (SELECT 1 FROM bill.BillRun r WHERE r.CycleCode = c.CycleCode AND r.Stage NOT IN (5, 10))
ORDER BY c.NextCutOff, c.CycleCode";
            using (var connection = Open())
            using (var command = connection.Command(sql).WithDate("@today", today))
            {
                return ReadCycles(command);
            }
        }

        public BillCycle GetCycle(string cycleCode)
        {
            const string sql = @"SELECT CycleCode, Name, IntervalMonths, NextCutOff, ReadWindowDays, PaymentDays FROM bill.BillCycle WHERE CycleCode = @code";
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@code", cycleCode))
            {
                return ReadCycles(command).SingleOrDefault();
            }
        }

        public int CreateRun(BillCycle cycle, DateTime billDate)
        {
            const string sql = @"
INSERT bill.BillRun (CycleCode, CutOffDate, BillDate, Stage) OUTPUT inserted.RunId VALUES (@cycle, @cutOff, @billDate, 0)";
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@cycle", cycle.CycleCode).WithDate("@cutOff", cycle.NextCutOff).WithDate("@billDate", billDate))
            {
                return (int)command.ExecuteScalar();
            }
        }

        public List<BillRunSummary> GetRuns(int days)
        {
            const string sql = @"
SELECT * FROM bill.RunOverview
WHERE CreatedAt >= DATEADD(day, -@days, SYSDATETIME()) OR Stage NOT IN (5, 10)
ORDER BY RunId DESC";
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@days", days))
            {
                return ReadRuns(command);
            }
        }

        public List<BillRunSummary> GetRunsInStage(params BillRunStage[] stages)
        {
            var sql = "SELECT * FROM bill.RunOverview WHERE Stage IN (" + string.Join(",", stages.Select(s => ((int)s).ToString())) + ") ORDER BY RunId";
            using (var connection = Open())
            using (var command = connection.Command(sql))
            {
                return ReadRuns(command);
            }
        }

        public BillRunSummary GetRun(int runId)
        {
            using (var connection = Open())
            using (var command = connection.Command("SELECT * FROM bill.RunOverview WHERE RunId = @id").With("@id", runId))
            {
                return ReadRuns(command).SingleOrDefault();
            }
        }

        public void SetStage(int runId, BillRunStage stage, string message, string userName = null)
        {
            const string sql = @"
UPDATE bill.BillRun SET
    Stage = @stage,
    Message = @message,
    HeldBy = CASE WHEN @stage = 8 THEN @user WHEN @stage IN (0, 1, 2, 3) THEN NULL ELSE HeldBy END,
    ApprovedBy = CASE WHEN @stage = 3 THEN ISNULL(@user, N'(automatic)') WHEN @stage IN (0, 1, 2) THEN NULL ELSE ApprovedBy END
WHERE RunId = @id";
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@id", runId).With("@stage", (byte)stage).With("@message", message).With("@user", userName))
            {
                command.ExecuteNonQuery();
            }

            AddEvent(runId, "Info", userName, "Stage " + stage + (string.IsNullOrEmpty(message) ? string.Empty : ": " + message));
        }

        public void AddEvent(int? runId, string level, string userName, string message)
        {
            const string sql = "INSERT bill.RunEvent (RunId, Level, UserName, Message) VALUES (@run, @level, @user, @message)";
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@run", runId).With("@level", level).With("@user", userName)
                       .With("@message", message.Length > 1000 ? message.Substring(0, 1000) : message))
            {
                command.ExecuteNonQuery();
            }
        }

        public List<RunEvent> GetRunEvents(int runId)
        {
            const string sql = "SELECT At, Level, UserName, Message FROM bill.RunEvent WHERE RunId = @run ORDER BY EventId";
            var events = new List<RunEvent>();
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@run", runId))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    events.Add(new RunEvent
                    {
                        At = reader.GetDateTime(0),
                        Level = reader.GetString(1),
                        UserName = reader.GetStringOrNull("UserName"),
                        Message = reader.GetString(3)
                    });
                }
            }

            return events;
        }

        // ---- accounts, tariffs, reads ----

        private const string AccountSelect = @"
SELECT a.AccountNumber, a.CustomerName, a.AddressLine1, a.AddressLine2, a.PostalCode, a.City, a.TariffCode, a.CycleCode,
       a.PaymentMethod, a.Iban, a.MandateId, a.MandateSignedOn, a.FirstCollection, a.AverageDailyUse,
       m.Serial, m.Dials, m.Multiplier, m.SizeCode,
       r.ReadId, r.ReadDate, r.Reading, r.ReadType, r.Source, r.ReaderCode, r.Status
FROM bill.Account a
JOIN bill.Meter m ON m.Serial = a.MeterSerial
LEFT JOIN bill.MeterRead r ON r.ReadId = a.LastBilledReadId";

        public List<BillingAccount> GetAccounts(string cycleCode)
        {
            using (var connection = Open())
            using (var command = connection.Command(AccountSelect + " WHERE a.CycleCode = @cycle AND a.Active = 1 ORDER BY a.AccountNumber").With("@cycle", cycleCode))
            {
                return ReadAccounts(command);
            }
        }

        public BillingAccount GetAccount(string accountNumber)
        {
            using (var connection = Open())
            using (var command = connection.Command(AccountSelect + " WHERE a.AccountNumber = @account").With("@account", accountNumber))
            {
                return ReadAccounts(command).SingleOrDefault();
            }
        }

        public List<Tariff> GetTariffs()
        {
            var tariffs = new Dictionary<int, Tariff>();
            using (var connection = Open())
            {
                using (var command = connection.Command("SELECT TariffId, Code, Name, ValidFrom, StandingChargePerDay, WastewaterPrice, WastewaterShare, VatRate FROM bill.Tariff"))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tariffs.Add(reader.GetInt32(0), new Tariff
                        {
                            Code = reader.GetString(1),
                            Name = reader.GetString(2),
                            ValidFrom = reader.GetDateTime(3),
                            StandingChargePerDay = reader.GetDecimal(4),
                            WastewaterPrice = reader.GetDecimal(5),
                            WastewaterShare = reader.GetDecimal(6),
                            VatRate = reader.GetDecimal(7)
                        });
                    }
                }

                using (var command = connection.Command("SELECT TariffId, UpTo, Price FROM bill.TariffBlock ORDER BY TariffId, BlockNo"))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tariffs[reader.GetInt32(0)].WaterBlocks.Add(new TariffBlock
                        {
                            UpTo = reader.GetNullable<decimal>("UpTo"),
                            Price = reader.GetDecimal(2)
                        });
                    }
                }
            }

            return tariffs.Values.ToList();
        }

        public HashSet<string> GetMeterSerials()
        {
            var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = Open())
            using (var command = connection.Command("SELECT Serial FROM bill.Meter"))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    serials.Add(reader.GetString(0));
                }
            }

            return serials;
        }

        /// <summary>Latest new read of the meter after the last billed read and on or before the cut-off.</summary>
        public StoredRead FindNewRead(string meterSerial, DateTime after, DateTime cutOff)
        {
            const string sql = @"
SELECT TOP (1) ReadId, MeterSerial, ReadDate, Reading, ReadType, Source, ReaderCode, Route, ReaderId, Status
FROM bill.MeterRead
WHERE MeterSerial = @meter AND Status = 0 AND ReadType <> 2 AND ReadDate > @after AND ReadDate <= @cutOff
ORDER BY ReadDate DESC, ReadId DESC";
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@meter", meterSerial).WithDate("@after", after).WithDate("@cutOff", cutOff))
            using (var reader = command.ExecuteReader())
            {
                return reader.Read() ? ReadStoredRead(reader) : null;
            }
        }

        public List<StoredRead> GetReads(string meterSerial, int top)
        {
            const string sql = @"
SELECT TOP (@top) ReadId, MeterSerial, ReadDate, Reading, ReadType, Source, ReaderCode, Route, ReaderId, Status
FROM bill.MeterRead WHERE MeterSerial = @meter ORDER BY ReadDate DESC, ReadId DESC";
            var reads = new List<StoredRead>();
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@meter", meterSerial).With("@top", top))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    reads.Add(ReadStoredRead(reader));
                }
            }

            return reads;
        }

        public int InsertRead(SqlConnection connection, SqlTransaction transaction, MeterRead read, int? importFileId)
        {
            const string sql = @"
INSERT bill.MeterRead (MeterSerial, ReadDate, Reading, ReadType, Source, ReaderCode, Route, ReaderId, ImportFileId)
OUTPUT inserted.ReadId
VALUES (@meter, @date, @reading, @type, @source, @code, @route, @reader, @file)";
            using (var command = connection.Command(sql, transaction)
                       .With("@meter", read.MeterSerial).WithDate("@date", read.ReadDate).With("@reading", read.Reading)
                       .With("@type", (byte)read.ReadType).With("@source", (byte)read.Source).With("@code", read.ReaderCode ?? "00")
                       .With("@route", read.Route).With("@reader", read.ReaderId).With("@file", importFileId))
            {
                return (int)command.ExecuteScalar();
            }
        }

        // ---- import files ----

        public ImportFileRecord FindImportByHash(string sha256)
        {
            using (var connection = Open())
            using (var command = connection.Command("SELECT TOP (1) * FROM bill.ImportFile WHERE Sha256 = @hash AND Status = 0 ORDER BY ImportFileId").With("@hash", sha256))
            {
                return ReadImports(command).FirstOrDefault();
            }
        }

        public int SaveImport(string fileName, ReadSource source, string batchId, string sha256, int status, string message, IList<MeterRead> reads, int rejected)
        {
            const string sql = @"
INSERT bill.ImportFile (FileName, Source, BatchId, Sha256, ReadCount, RejectedCount, Status, Message)
OUTPUT inserted.ImportFileId
VALUES (@name, @source, @batch, @hash, @reads, @rejected, @status, @message)";
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction())
            {
                int id;
                using (var command = connection.Command(sql, transaction)
                           .With("@name", fileName).With("@source", (byte)source).With("@batch", batchId).With("@hash", sha256)
                           .With("@reads", reads.Count).With("@rejected", rejected).With("@status", (byte)status).With("@message", message))
                {
                    id = (int)command.ExecuteScalar();
                }

                foreach (var read in reads)
                {
                    InsertRead(connection, transaction, read, id);
                }

                transaction.Commit();
                return id;
            }
        }

        public List<ImportFileRecord> GetImportFiles(int days)
        {
            using (var connection = Open())
            using (var command = connection.Command("SELECT * FROM bill.ImportFile WHERE ReceivedAt >= DATEADD(day, -@days, SYSDATETIME()) ORDER BY ImportFileId DESC").With("@days", days))
            {
                return ReadImports(command);
            }
        }

        // ---- rating ----

        /// <summary>Removes the draft bills, the engine estimates and the open exceptions of a run before it is rated again.</summary>
        public void ClearDrafts(int runId)
        {
            const string sql = @"
DECLARE @reads TABLE (ReadId int PRIMARY KEY);
INSERT @reads SELECT ClosingReadId FROM bill.Bill b JOIN bill.MeterRead r ON r.ReadId = b.ClosingReadId
WHERE b.RunId = @run AND b.Status <> 1 AND r.Source = 3 AND r.Status = 0;
DELETE bill.Bill WHERE RunId = @run AND Status <> 1;
DELETE bill.MeterRead WHERE ReadId IN (SELECT ReadId FROM @reads);
DELETE bill.BillException WHERE RunId = @run AND Resolution = 0;";
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@run", runId))
            {
                command.ExecuteNonQuery();
            }
        }

        public void SaveDraft(int runId, Bill bill, int openingReadId, MeterRead closingRead, int? closingReadId, int status, IEnumerable<BillException> exceptions)
        {
            const string billSql = @"
INSERT bill.Bill (RunId, BillNumber, AccountNumber, BillDate, DueDate, PeriodFrom, PeriodTo, OpeningReadId, ClosingReadId,
                  OpeningReading, ClosingReading, Consumption, Estimated, NetAmount, VatAmount, TotalAmount, PaymentReference, Status)
OUTPUT inserted.BillId
VALUES (@run, @number, @account, @billDate, @dueDate, @from, @to, @opening, @closing,
        @openingReading, @closingReading, @consumption, @estimated, @net, @vat, @total, @reference, @status)";
            const string lineSql = @"
INSERT bill.BillLine (BillId, LineNumber, Code, Description, Quantity, Unit, UnitPrice, Amount, VatRate)
VALUES (@bill, @line, @code, @description, @quantity, @unit, @price, @amount, @vat)";
            const string exceptionSql = @"
INSERT bill.BillException (RunId, AccountNumber, ReadId, Code, Consumption, Expected, Resolution, ResolvedBy, ResolvedAt, Note)
VALUES (@run, @account, @read, @code, @consumption, @expected, 0, NULL, NULL, NULL)";

            using (var connection = Open())
            using (var transaction = connection.BeginTransaction())
            {
                var readId = closingReadId ?? InsertRead(connection, transaction, closingRead, null);

                int billId;
                using (var command = connection.Command(billSql, transaction)
                           .With("@run", runId).With("@number", bill.BillNumber).With("@account", bill.AccountNumber)
                           .WithDate("@billDate", bill.BillDate).WithDate("@dueDate", bill.DueDate)
                           .WithDate("@from", bill.PeriodFrom).WithDate("@to", bill.PeriodTo)
                           .With("@opening", openingReadId).With("@closing", readId)
                           .With("@openingReading", bill.OpeningReading).With("@closingReading", bill.ClosingReading)
                           .With("@consumption", bill.Consumption).With("@estimated", bill.Estimated)
                           .With("@net", bill.NetAmount).With("@vat", bill.VatAmount).With("@total", bill.TotalAmount)
                           .With("@reference", bill.PaymentReference).With("@status", (byte)status))
                {
                    billId = (int)command.ExecuteScalar();
                }

                var lineNumber = 1;
                foreach (var line in bill.Lines)
                {
                    using (var command = connection.Command(lineSql, transaction)
                               .With("@bill", billId).With("@line", (short)lineNumber++).With("@code", line.Code)
                               .With("@description", line.Description).With("@quantity", line.Quantity).With("@unit", line.Unit)
                               .With("@price", line.UnitPrice).With("@amount", line.Amount).With("@vat", line.VatRate))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                foreach (var exception in exceptions)
                {
                    using (var command = connection.Command(exceptionSql, transaction)
                               .With("@run", runId).With("@account", bill.AccountNumber).With("@read", readId)
                               .With("@code", exception.Code).With("@consumption", exception.Consumption).With("@expected", exception.Expected))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
        }

        public List<BillException> GetExceptions(int runId, bool openOnly)
        {
            var sql = @"
SELECT e.*, a.CustomerName FROM bill.BillException e JOIN bill.Account a ON a.AccountNumber = e.AccountNumber
WHERE e.RunId = @run" + (openOnly ? " AND e.Resolution = 0" : string.Empty) + " ORDER BY e.AccountNumber, e.ExceptionId";
            var list = new List<BillException>();
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@run", runId))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new BillException
                    {
                        ExceptionId = (int)reader["ExceptionId"],
                        RunId = (int)reader["RunId"],
                        AccountNumber = (string)reader["AccountNumber"],
                        CustomerName = (string)reader["CustomerName"],
                        ReadId = reader.GetNullable<int>("ReadId"),
                        Code = (string)reader["Code"],
                        Consumption = reader.GetNullable<decimal>("Consumption"),
                        Expected = reader.GetNullable<decimal>("Expected"),
                        Resolution = (ExceptionResolution)(byte)reader["Resolution"],
                        ResolvedBy = reader.GetStringOrNull("ResolvedBy"),
                        ResolvedAt = reader.GetNullable<DateTime>("ResolvedAt"),
                        Note = reader.GetStringOrNull("Note")
                    });
                }
            }

            return list;
        }

        /// <summary>Resolutions already made in a run, by account; they are kept when the run is rated again.</summary>
        public Dictionary<string, ExceptionResolution> GetResolutions(int runId)
        {
            const string sql = @"
SELECT AccountNumber, MAX(Resolution) FROM bill.BillException WHERE RunId = @run AND Resolution <> 0 GROUP BY AccountNumber";
            var map = new Dictionary<string, ExceptionResolution>(StringComparer.OrdinalIgnoreCase);
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@run", runId))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    map[reader.GetString(0)] = (ExceptionResolution)reader.GetByte(1);
                }
            }

            return map;
        }

        public BillException ResolveException(int exceptionId, ExceptionResolution resolution, string userName, string note)
        {
            const string sql = @"
UPDATE bill.BillException SET Resolution = @resolution, ResolvedBy = @user, ResolvedAt = SYSDATETIME(), Note = @note
OUTPUT inserted.RunId, inserted.AccountNumber, inserted.Code
WHERE ExceptionId = @id AND Resolution = 0";
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@id", exceptionId).With("@resolution", (byte)resolution).With("@user", userName).With("@note", note))
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    return null;
                }

                return new BillException { ExceptionId = exceptionId, RunId = reader.GetInt32(0), AccountNumber = reader.GetString(1), Code = reader.GetString(2), Resolution = resolution };
            }
        }

        // ---- release ----

        public List<Bill> GetBills(int runId, int status)
        {
            using (var connection = Open())
            using (var command = connection.Command("SELECT * FROM bill.Bill WHERE RunId = @run AND Status = @status ORDER BY BillNumber").With("@run", runId).With("@status", (byte)status))
            {
                return ReadBills(connection, command);
            }
        }

        public Bill GetBill(string billNumber)
        {
            using (var connection = Open())
            using (var command = connection.Command("SELECT * FROM bill.Bill WHERE BillNumber = @number").With("@number", billNumber))
            {
                return ReadBills(connection, command).SingleOrDefault();
            }
        }

        public void CompleteRun(int runId, string printFile, string debitFile)
        {
            const string sql = @"
DECLARE @issued TABLE (AccountNumber varchar(10), ClosingReadId int, ReadDate date, MeterSerial varchar(12));

UPDATE b SET Status = 1, IssuedAt = SYSDATETIME()
OUTPUT inserted.AccountNumber, inserted.ClosingReadId, r.ReadDate, r.MeterSerial INTO @issued
FROM bill.Bill b JOIN bill.MeterRead r ON r.ReadId = b.ClosingReadId
WHERE b.RunId = @run AND b.Status = 0;

UPDATE r SET Status = 1 FROM bill.MeterRead r JOIN @issued i ON i.ClosingReadId = r.ReadId;

UPDATE r SET Status = 3 FROM bill.MeterRead r JOIN @issued i ON i.MeterSerial = r.MeterSerial
WHERE r.Status = 0 AND r.ReadDate <= i.ReadDate;

UPDATE a SET LastBilledReadId = i.ClosingReadId, FirstCollection = CASE WHEN a.PaymentMethod = 1 THEN 0 ELSE a.FirstCollection END
FROM bill.Account a JOIN @issued i ON i.AccountNumber = a.AccountNumber;

UPDATE c SET NextCutOff = EOMONTH(DATEADD(month, c.IntervalMonths, r.CutOffDate))
FROM bill.BillCycle c JOIN bill.BillRun r ON r.CycleCode = c.CycleCode
WHERE r.RunId = @run;

UPDATE bill.BillRun SET Stage = 5, CompletedAt = SYSDATETIME(), PrintFile = @print, DebitFile = @debit, Message = NULL WHERE RunId = @run;";
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                using (var command = connection.Command(sql, transaction).With("@run", runId).With("@print", printFile).With("@debit", debitFile))
                {
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }

            AddEvent(runId, "Info", null, "Stage Completed");
        }

        // ---- reprints ----

        public int AddReprintRequest(string billNumber, string requestedBy, string reason)
        {
            const string sql = "INSERT bill.ReprintRequest (BillNumber, RequestedBy, Reason) OUTPUT inserted.RequestId VALUES (@bill, @user, @reason)";
            using (var connection = Open())
            using (var command = connection.Command(sql).With("@bill", billNumber).With("@user", requestedBy).With("@reason", reason))
            {
                return (int)command.ExecuteScalar();
            }
        }

        public List<ReprintRequest> GetPendingReprints()
        {
            const string sql = "SELECT RequestId, BillNumber, RequestedBy, RequestedAt, Reason FROM bill.ReprintRequest WHERE ProcessedAt IS NULL ORDER BY RequestId";
            var list = new List<ReprintRequest>();
            using (var connection = Open())
            using (var command = connection.Command(sql))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new ReprintRequest
                    {
                        RequestId = reader.GetInt32(0),
                        BillNumber = reader.GetString(1),
                        RequestedBy = reader.GetString(2),
                        RequestedAt = reader.GetDateTime(3),
                        Reason = reader.GetStringOrNull("Reason")
                    });
                }
            }

            return list;
        }

        public void MarkReprinted(IEnumerable<int> requestIds, string fileName)
        {
            using (var connection = Open())
            {
                foreach (var id in requestIds)
                {
                    using (var command = connection.Command("UPDATE bill.ReprintRequest SET ProcessedAt = SYSDATETIME(), FileName = @file WHERE RequestId = @id")
                               .With("@id", id).With("@file", fileName))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        // ---- readers ----

        private static List<BillCycle> ReadCycles(SqlCommand command)
        {
            var list = new List<BillCycle>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new BillCycle
                    {
                        CycleCode = reader.GetString(0),
                        Name = reader.GetString(1),
                        IntervalMonths = reader.GetByte(2),
                        NextCutOff = reader.GetDateTime(3),
                        ReadWindowDays = reader.GetInt16(4),
                        PaymentDays = reader.GetInt16(5)
                    });
                }
            }

            return list;
        }

        private static List<BillRunSummary> ReadRuns(SqlCommand command)
        {
            var list = new List<BillRunSummary>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new BillRunSummary
                    {
                        RunId = (int)reader["RunId"],
                        CycleCode = (string)reader["CycleCode"],
                        CutOffDate = (DateTime)reader["CutOffDate"],
                        BillDate = (DateTime)reader["BillDate"],
                        Stage = (BillRunStage)(byte)reader["Stage"],
                        CreatedAt = (DateTime)reader["CreatedAt"],
                        CompletedAt = reader.GetNullable<DateTime>("CompletedAt"),
                        HeldBy = reader.GetStringOrNull("HeldBy"),
                        Message = reader.GetStringOrNull("Message"),
                        Accounts = (int)reader["Accounts"],
                        Bills = (int)reader["Bills"],
                        EstimatedBills = (int)reader["EstimatedBills"],
                        TotalAmount = (decimal)reader["TotalAmount"],
                        OpenExceptions = (int)reader["OpenExceptions"]
                    });
                }
            }

            return list;
        }

        private static List<BillingAccount> ReadAccounts(SqlCommand command)
        {
            var list = new List<BillingAccount>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var account = new BillingAccount
                    {
                        AccountNumber = (string)reader["AccountNumber"],
                        CustomerName = (string)reader["CustomerName"],
                        AddressLine1 = (string)reader["AddressLine1"],
                        AddressLine2 = reader.GetStringOrNull("AddressLine2"),
                        PostalCode = (string)reader["PostalCode"],
                        City = (string)reader["City"],
                        TariffCode = (string)reader["TariffCode"],
                        CycleCode = (string)reader["CycleCode"],
                        PaymentMethod = (PaymentMethod)(byte)reader["PaymentMethod"],
                        Iban = reader.GetStringOrNull("Iban"),
                        MandateId = reader.GetStringOrNull("MandateId"),
                        MandateSignedOn = reader.GetNullable<DateTime>("MandateSignedOn"),
                        FirstCollection = (bool)reader["FirstCollection"],
                        AverageDailyUse = (decimal)reader["AverageDailyUse"],
                        Meter = new Meter
                        {
                            Serial = (string)reader["Serial"],
                            Dials = (byte)reader["Dials"],
                            Multiplier = (decimal)reader["Multiplier"],
                            SizeCode = (string)reader["SizeCode"]
                        }
                    };

                    if (reader["ReadId"] != DBNull.Value)
                    {
                        account.LastBilledRead = new StoredRead
                        {
                            ReadId = (int)reader["ReadId"],
                            MeterSerial = account.Meter.Serial,
                            ReadDate = (DateTime)reader["ReadDate"],
                            Reading = (decimal)reader["Reading"],
                            ReadType = (ReadType)(byte)reader["ReadType"],
                            Source = (ReadSource)(byte)reader["Source"],
                            ReaderCode = (string)reader["ReaderCode"],
                            Status = (byte)reader["Status"]
                        };
                    }

                    list.Add(account);
                }
            }

            return list;
        }

        private static StoredRead ReadStoredRead(IDataRecord reader)
        {
            return new StoredRead
            {
                ReadId = (int)reader["ReadId"],
                MeterSerial = (string)reader["MeterSerial"],
                ReadDate = (DateTime)reader["ReadDate"],
                Reading = (decimal)reader["Reading"],
                ReadType = (ReadType)(byte)reader["ReadType"],
                Source = (ReadSource)(byte)reader["Source"],
                ReaderCode = (string)reader["ReaderCode"],
                Route = reader.GetStringOrNull("Route"),
                ReaderId = reader.GetStringOrNull("ReaderId"),
                Status = (byte)reader["Status"]
            };
        }

        private static List<ImportFileRecord> ReadImports(SqlCommand command)
        {
            var list = new List<ImportFileRecord>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new ImportFileRecord
                    {
                        ImportFileId = (int)reader["ImportFileId"],
                        FileName = (string)reader["FileName"],
                        Source = (byte)reader["Source"],
                        BatchId = reader.GetStringOrNull("BatchId"),
                        ReceivedAt = (DateTime)reader["ReceivedAt"],
                        ReadCount = (int)reader["ReadCount"],
                        RejectedCount = (int)reader["RejectedCount"],
                        Status = (byte)reader["Status"],
                        Message = reader.GetStringOrNull("Message")
                    });
                }
            }

            return list;
        }

        private static List<Bill> ReadBills(SqlConnection connection, SqlCommand command)
        {
            var bills = new Dictionary<int, Bill>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    bills.Add((int)reader["BillId"], new Bill
                    {
                        BillNumber = (string)reader["BillNumber"],
                        AccountNumber = (string)reader["AccountNumber"],
                        BillDate = (DateTime)reader["BillDate"],
                        DueDate = (DateTime)reader["DueDate"],
                        PeriodFrom = (DateTime)reader["PeriodFrom"],
                        PeriodTo = (DateTime)reader["PeriodTo"],
                        OpeningReading = (decimal)reader["OpeningReading"],
                        ClosingReading = (decimal)reader["ClosingReading"],
                        Consumption = (decimal)reader["Consumption"],
                        Estimated = (bool)reader["Estimated"],
                        NetAmount = (decimal)reader["NetAmount"],
                        VatAmount = (decimal)reader["VatAmount"],
                        TotalAmount = (decimal)reader["TotalAmount"],
                        PaymentReference = (string)reader["PaymentReference"]
                    });
                }
            }

            if (bills.Count == 0)
            {
                return new List<Bill>();
            }

            var ids = string.Join(",", bills.Keys);
            using (var lines = connection.Command("SELECT * FROM bill.BillLine WHERE BillId IN (" + ids + ") ORDER BY BillId, LineNumber"))
            using (var reader = lines.ExecuteReader())
            {
                while (reader.Read())
                {
                    bills[(int)reader["BillId"]].Lines.Add(new BillLine
                    {
                        Code = (string)reader["Code"],
                        Description = (string)reader["Description"],
                        Quantity = (decimal)reader["Quantity"],
                        Unit = (string)reader["Unit"],
                        UnitPrice = (decimal)reader["UnitPrice"],
                        Amount = (decimal)reader["Amount"],
                        VatRate = (decimal)reader["VatRate"]
                    });
                }
            }

            return bills.Values.ToList();
        }
    }
}
