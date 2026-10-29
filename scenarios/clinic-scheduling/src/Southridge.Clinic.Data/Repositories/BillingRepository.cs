using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Southridge.Clinic.Data.Repositories
{
    public class BillingRepository : RepositoryBase
    {
        public List<ReadyEncounter> ReadyEncounters(DateTime asOf, int lagDays)
        {
            DbCommand cmd = Proc("dbo.usp_Billing_ReadyEncounters");
            In(cmd, "@AsOf", DbType.DateTime, asOf);
            In(cmd, "@LagDays", DbType.Int32, lagDays);
            var list = new List<ReadyEncounter>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new ReadyEncounter
                    {
                        EncounterId = GetInt(r, "EncounterId"),
                        DateOfService = GetDate(r, "DateOfService"),
                        PatientId = GetInt(r, "PatientId"),
                        Mrn = GetString(r, "Mrn"),
                        PatientName = GetString(r, "LastName") + ", " + GetString(r, "FirstName"),
                        PatientInsuranceId = GetNullableInt(r, "PatientInsuranceId"),
                        PayerId = GetNullableInt(r, "PayerId"),
                        PayerCode = GetString(r, "PayerCode"),
                        PayerType = r["PayerType"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PayerType"]),
                        TotalCharge = GetDecimal(r, "TotalCharge"),
                        ChargeCount = GetInt(r, "ChargeCount")
                    });
                }
            }

            return list;
        }

        public int CreateClaim(int encounterId, int patientInsuranceId, out string claimNumber)
        {
            DbCommand cmd = Proc("dbo.usp_Claim_Create");
            In(cmd, "@EncounterId", DbType.Int32, encounterId);
            In(cmd, "@PatientInsuranceId", DbType.Int32, patientInsuranceId);
            Out(cmd, "@ClaimId", DbType.Int32, 4);
            Out(cmd, "@ClaimNumber", DbType.AnsiString, 20);
            Db.ExecuteNonQuery(cmd);
            claimNumber = OutValue<string>(cmd, "@ClaimNumber");
            return OutValue<int>(cmd, "@ClaimId");
        }

        /// <summary>Self-pay visit: the charges go straight to the patient balance (no claim).</summary>
        public decimal BillToPatient(int encounterId)
        {
            DbCommand cmd = Db.GetSqlStringCommand(
                "SET XACT_ABORT ON; BEGIN TRANSACTION; " +
                "DECLARE @total money = (SELECT ISNULL(SUM(Amount * Units), 0) FROM dbo.Charge WHERE EncounterId = @EncounterId AND Status IN (0, 1) AND ClaimId IS NULL); " +
                "UPDATE dbo.Charge SET Status = 2, PatientAmount = Amount * Units WHERE EncounterId = @EncounterId AND Status IN (0, 1) AND ClaimId IS NULL; " +
                "UPDATE dbo.Encounter SET Status = 2 WHERE EncounterId = @EncounterId; " +
                "UPDATE p SET Balance = Balance + @total FROM dbo.Patient p JOIN dbo.Encounter e ON e.PatientId = p.PatientId WHERE e.EncounterId = @EncounterId; " +
                "COMMIT; SELECT @total;");
            In(cmd, "@EncounterId", DbType.Int32, encounterId);
            return Convert.ToDecimal(Db.ExecuteScalar(cmd), CultureInfo.InvariantCulture);
        }

        /// <summary>Claims waiting for the next file: new ones and those put back after a rejection.</summary>
        public List<int> ClaimsToSend()
        {
            DbCommand cmd = Db.GetSqlStringCommand("SELECT ClaimId FROM dbo.Claim WHERE Status = 0 AND ClaimBatchId IS NULL ORDER BY ClaimId");
            var list = new List<int>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(r.GetInt32(0));
                }
            }

            return list;
        }

        public void RejectClaim(int claimId, string reason)
        {
            DbCommand cmd = Proc("dbo.usp_Claim_Reject");
            In(cmd, "@ClaimId", DbType.Int32, claimId);
            In(cmd, "@Reason", DbType.String, reason);
            Db.ExecuteNonQuery(cmd);
        }

        public ClaimData GetClaimData(int claimId)
        {
            DbCommand cmd = Proc("dbo.usp_Claim_BuildData");
            In(cmd, "@ClaimId", DbType.Int32, claimId);
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                if (!r.Read())
                {
                    return null;
                }

                var c = new ClaimData
                {
                    ClaimId = GetInt(r, "ClaimId"),
                    ClaimNumber = GetString(r, "ClaimNumber"),
                    DateOfService = GetDate(r, "DateOfService"),
                    TotalCharge = GetDecimal(r, "TotalCharge"),
                    FrequencyCode = GetString(r, "FrequencyCode"),
                    OriginalClaimNumber = GetString(r, "OriginalClaimNumber"),
                    PayerClaimNumber = GetString(r, "PayerClaimNumber"),
                    Mrn = GetString(r, "Mrn"),
                    PatientFirstName = GetString(r, "FirstName"),
                    PatientMiddleName = GetString(r, "MiddleName"),
                    PatientLastName = GetString(r, "LastName"),
                    PatientDateOfBirth = GetDate(r, "DateOfBirth"),
                    PatientSex = Convert.ToByte(r["Sex"]),
                    PatientAddress1 = GetString(r, "Address1"),
                    PatientAddress2 = GetString(r, "Address2"),
                    PatientCity = GetString(r, "City"),
                    PatientState = GetString(r, "State"),
                    PatientZip = GetString(r, "Zip"),
                    MemberId = GetString(r, "MemberId"),
                    GroupNumber = GetString(r, "GroupNumber"),
                    Relationship = Convert.ToByte(r["Relationship"]),
                    SubscriberFirstName = GetString(r, "SubscriberFirstName"),
                    SubscriberLastName = GetString(r, "SubscriberLastName"),
                    SubscriberDateOfBirth = GetNullableDate(r, "SubscriberDateOfBirth"),
                    PayerName = GetString(r, "PayerName"),
                    ClaimPayerId = GetString(r, "ClaimPayerId"),
                    FilingIndicator = GetString(r, "FilingIndicator"),
                    PayerType = Convert.ToInt32(r["PayerType"]),
                    PayerAddress1 = GetString(r, "PayerAddress1"),
                    PayerCity = GetString(r, "PayerCity"),
                    PayerState = GetString(r, "PayerState"),
                    PayerZip = GetString(r, "PayerZip"),
                    RenderingNpi = GetString(r, "RenderingNpi"),
                    RenderingFirstName = GetString(r, "RenderingFirstName"),
                    RenderingLastName = GetString(r, "RenderingLastName"),
                    TaxonomyCode = GetString(r, "TaxonomyCode"),
                    SiteName = GetString(r, "SiteName"),
                    SiteAddress1 = GetString(r, "SiteAddress1"),
                    SiteCity = GetString(r, "SiteCity"),
                    SiteState = GetString(r, "SiteState"),
                    SiteZip = GetString(r, "SiteZip"),
                    PlaceOfService = GetString(r, "PlaceOfService"),
                    TaxId = GetString(r, "TaxId"),
                    GroupNpi = GetString(r, "GroupNpi"),
                    AuthorizationNumber = GetString(r, "AuthorizationNumber")
                };

                r.NextResult();
                while (r.Read())
                {
                    c.Diagnoses.Add(new ClaimDiagnosis { Sequence = Convert.ToInt32(r["Sequence"]), Code = GetString(r, "Code"), CodeSet = Convert.ToInt32(r["CodeSet"]) });
                }

                r.NextResult();
                while (r.Read())
                {
                    c.Lines.Add(new ClaimLine
                    {
                        ChargeId = GetInt(r, "ChargeId"),
                        LineNumber = Convert.ToInt32(r["LineNumber"]),
                        CptCode = GetString(r, "CptCode"),
                        Modifier1 = GetString(r, "Modifier1"),
                        Modifier2 = GetString(r, "Modifier2"),
                        Units = GetInt(r, "Units"),
                        Amount = GetDecimal(r, "Amount"),
                        DiagnosisPointers = GetString(r, "DiagnosisPointers")
                    });
                }

                return c;
            }
        }

        public int CreateBatch(string fileName, IEnumerable<int> claimIds, out int interchangeControlNumber)
        {
            var ids = new List<string>();
            foreach (int id in claimIds)
            {
                ids.Add(id.ToString(CultureInfo.InvariantCulture));
            }

            DbCommand cmd = Proc("dbo.usp_ClaimBatch_Create");
            In(cmd, "@FileName", DbType.AnsiString, fileName);
            In(cmd, "@ClaimIds", DbType.AnsiString, string.Join(",", ids.ToArray()));
            Out(cmd, "@ClaimBatchId", DbType.Int32, 4);
            Out(cmd, "@InterchangeControlNumber", DbType.Int32, 4);
            Db.ExecuteNonQuery(cmd);
            interchangeControlNumber = OutValue<int>(cmd, "@InterchangeControlNumber");
            return OutValue<int>(cmd, "@ClaimBatchId");
        }

        public void RenameBatchFile(int claimBatchId, string fileName)
        {
            DbCommand cmd = Db.GetSqlStringCommand("UPDATE dbo.ClaimBatch SET FileName = @FileName WHERE ClaimBatchId = @ClaimBatchId");
            In(cmd, "@FileName", DbType.AnsiString, fileName);
            In(cmd, "@ClaimBatchId", DbType.Int32, claimBatchId);
            Db.ExecuteNonQuery(cmd);
        }

        public void MarkBatchSent(int claimBatchId)
        {
            DbCommand cmd = Proc("dbo.usp_ClaimBatch_MarkSent");
            In(cmd, "@ClaimBatchId", DbType.Int32, claimBatchId);
            Db.ExecuteNonQuery(cmd);
        }

        public int AcknowledgeBatch(int interchangeControlNumber, string ackStatus, string ackFileName)
        {
            DbCommand cmd = Proc("dbo.usp_ClaimBatch_Acknowledge");
            In(cmd, "@InterchangeControlNumber", DbType.Int32, interchangeControlNumber);
            In(cmd, "@AckStatus", DbType.AnsiStringFixedLength, ackStatus);
            In(cmd, "@AckFileName", DbType.AnsiString, ackFileName);
            Out(cmd, "@Updated", DbType.Int32, 4);
            Db.ExecuteNonQuery(cmd);
            return OutValue<int>(cmd, "@Updated");
        }

        public int InsertRemittance(string fileName, string payerName, string payerIdentifier, string traceNumber, string paymentMethod, DateTime paymentDate, decimal totalPaid, int claimCount)
        {
            DbCommand cmd = Proc("dbo.usp_Remittance_Insert");
            In(cmd, "@FileName", DbType.AnsiString, fileName);
            In(cmd, "@PayerName", DbType.String, payerName);
            In(cmd, "@PayerIdentifier", DbType.AnsiString, payerIdentifier);
            In(cmd, "@TraceNumber", DbType.AnsiString, traceNumber);
            In(cmd, "@PaymentMethod", DbType.AnsiStringFixedLength, paymentMethod);
            In(cmd, "@PaymentDate", DbType.Date, paymentDate);
            In(cmd, "@TotalPaid", DbType.Currency, totalPaid);
            In(cmd, "@ClaimCount", DbType.Int32, claimCount);
            Out(cmd, "@RemittanceId", DbType.Int32, 4);
            Db.ExecuteNonQuery(cmd);
            return OutValue<int>(cmd, "@RemittanceId");
        }

        public bool PostClaimPayment(int remittanceId, string claimNumber, string claimStatusCode, decimal chargeAmount, decimal paidAmount,
            decimal patientResponsibility, decimal adjustmentAmount, string payerClaimNumber, string reference)
        {
            DbCommand cmd = Proc("dbo.usp_Payment_PostClaim");
            In(cmd, "@RemittanceId", DbType.Int32, remittanceId);
            In(cmd, "@ClaimNumber", DbType.AnsiString, claimNumber);
            In(cmd, "@ClaimStatusCode", DbType.AnsiString, claimStatusCode);
            In(cmd, "@ChargeAmount", DbType.Currency, chargeAmount);
            In(cmd, "@PaidAmount", DbType.Currency, paidAmount);
            In(cmd, "@PatientResponsibility", DbType.Currency, patientResponsibility);
            In(cmd, "@AdjustmentAmount", DbType.Currency, adjustmentAmount);
            In(cmd, "@PayerClaimNumber", DbType.AnsiString, payerClaimNumber);
            In(cmd, "@Reference", DbType.AnsiString, reference);
            Out(cmd, "@Matched", DbType.Boolean, 1);
            Db.ExecuteNonQuery(cmd);
            return OutValue<bool>(cmd, "@Matched");
        }

        public List<StatementCandidate> StatementCandidates(DateTime statementDate, decimal minimumBalance)
        {
            DbCommand cmd = Proc("dbo.usp_Statement_Candidates");
            In(cmd, "@StatementDate", DbType.Date, statementDate);
            In(cmd, "@MinimumBalance", DbType.Currency, minimumBalance);
            var list = new List<StatementCandidate>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new StatementCandidate
                    {
                        PatientId = GetInt(r, "PatientId"),
                        Mrn = GetString(r, "Mrn"),
                        PatientName = GetString(r, "LastName") + ", " + GetString(r, "FirstName"),
                        Balance = GetDecimal(r, "Balance")
                    });
                }
            }

            return list;
        }

        public int InsertStatement(int patientId, DateTime statementDate, decimal previousBalance, decimal newCharges, decimal payments, decimal amountDue,
            DateTime dueDate, string fileName, int dunningLevel)
        {
            DbCommand cmd = Proc("dbo.usp_Statement_Insert");
            In(cmd, "@PatientId", DbType.Int32, patientId);
            In(cmd, "@StatementDate", DbType.Date, statementDate);
            In(cmd, "@PreviousBalance", DbType.Currency, previousBalance);
            In(cmd, "@NewCharges", DbType.Currency, newCharges);
            In(cmd, "@Payments", DbType.Currency, payments);
            In(cmd, "@AmountDue", DbType.Currency, amountDue);
            In(cmd, "@DueDate", DbType.Date, dueDate);
            In(cmd, "@FileName", DbType.String, fileName);
            In(cmd, "@DunningLevel", DbType.Byte, (byte)dunningLevel);
            Out(cmd, "@StatementId", DbType.Int32, 4);
            Db.ExecuteNonQuery(cmd);
            return OutValue<int>(cmd, "@StatementId");
        }

        public List<UnbilledEncounter> UnbilledEncounters(int days)
        {
            DbCommand cmd = Proc("dbo.usp_Encounter_FlagUnbilled");
            In(cmd, "@Days", DbType.Int32, days);
            var list = new List<UnbilledEncounter>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new UnbilledEncounter
                    {
                        EncounterId = GetInt(r, "EncounterId"),
                        DateOfService = GetDate(r, "DateOfService"),
                        Mrn = GetString(r, "Mrn"),
                        PatientName = GetString(r, "LastName") + ", " + GetString(r, "FirstName"),
                        ProviderName = GetString(r, "ProviderLastName"),
                        SiteCode = GetString(r, "SiteCode")
                    });
                }
            }

            return list;
        }

        public List<PatientClaimRow> ClaimsForPatient(int patientId, DateTime fromDate, DateTime toDate)
        {
            DbCommand cmd = Db.GetSqlStringCommand(
                @"SELECT c.ClaimId, c.ClaimNumber, c.DateOfService, py.Name AS PayerName, c.Status, c.TotalCharge, c.PaidAmount,
                         c.AdjustmentAmount, c.PatientResponsibility, c.RejectReason, c.SubmittedOn
                  FROM dbo.Claim c JOIN dbo.Payer py ON py.PayerId = c.PayerId
                  WHERE c.PatientId = @PatientId AND c.DateOfService BETWEEN @From AND @To
                  ORDER BY c.DateOfService DESC, c.ClaimNumber");
            In(cmd, "@PatientId", DbType.Int32, patientId);
            In(cmd, "@From", DbType.Date, fromDate);
            In(cmd, "@To", DbType.Date, toDate);
            var list = new List<PatientClaimRow>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new PatientClaimRow
                    {
                        ClaimId = GetInt(r, "ClaimId"),
                        ClaimNumber = GetString(r, "ClaimNumber"),
                        DateOfService = GetDate(r, "DateOfService"),
                        PayerName = GetString(r, "PayerName"),
                        Status = Convert.ToByte(r["Status"], CultureInfo.InvariantCulture),
                        TotalCharge = GetDecimal(r, "TotalCharge"),
                        PaidAmount = GetDecimal(r, "PaidAmount"),
                        AdjustmentAmount = GetDecimal(r, "AdjustmentAmount"),
                        PatientResponsibility = GetDecimal(r, "PatientResponsibility"),
                        RejectReason = GetString(r, "RejectReason"),
                        SubmittedOn = GetNullableDate(r, "SubmittedOn")
                    });
                }
            }

            return list;
        }

        public List<StatementRow> StatementsForPatient(int patientId, int maximumRows)
        {
            DbCommand cmd = Db.GetSqlStringCommand(
                @"SELECT TOP (@Max) StatementId, StatementDate, AmountDue, DueDate, DunningLevel, FileName
                  FROM dbo.Statement WHERE PatientId = @PatientId ORDER BY StatementDate DESC, StatementId DESC");
            In(cmd, "@Max", DbType.Int32, maximumRows);
            In(cmd, "@PatientId", DbType.Int32, patientId);
            var list = new List<StatementRow>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new StatementRow
                    {
                        StatementId = GetInt(r, "StatementId"),
                        StatementDate = GetDate(r, "StatementDate"),
                        AmountDue = GetDecimal(r, "AmountDue"),
                        DueDate = GetDate(r, "DueDate"),
                        DunningLevel = Convert.ToInt32(r["DunningLevel"], CultureInfo.InvariantCulture),
                        FileName = GetString(r, "FileName")
                    });
                }
            }

            return list;
        }
    }
}
