using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Hl7;

namespace Southridge.Clinic.Data.Repositories
{
    /// <summary>HL7 message log, outbound queue and the inbound updates (patients, lab results).</summary>
    public class InterfaceRepository : RepositoryBase
    {
        public int Enqueue(string partner, Hl7Message message, int? patientId)
        {
            DbCommand cmd = Proc("dbo.usp_Interface_Enqueue");
            In(cmd, "@Partner", DbType.AnsiString, partner);
            In(cmd, "@MessageType", DbType.AnsiString, message.TypeAndTrigger);
            In(cmd, "@ControlId", DbType.AnsiString, message.ControlId);
            In(cmd, "@PatientId", DbType.Int32, patientId);
            In(cmd, "@Payload", DbType.String, message.ToString());
            Out(cmd, "@MessageId", DbType.Int32, 4);
            Db.ExecuteNonQuery(cmd);
            return OutValue<int>(cmd, "@MessageId");
        }

        public List<OutboundMessage> Dequeue(string partner, int batchSize, int leaseSeconds)
        {
            DbCommand cmd = Proc("dbo.usp_Interface_Dequeue");
            In(cmd, "@Partner", DbType.AnsiString, partner);
            In(cmd, "@BatchSize", DbType.Int32, batchSize);
            In(cmd, "@LeaseSeconds", DbType.Int32, leaseSeconds);
            var list = new List<OutboundMessage>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new OutboundMessage
                    {
                        MessageId = GetInt(r, "MessageId"),
                        Attempts = GetInt(r, "Attempts"),
                        MessageType = GetString(r, "MessageType"),
                        ControlId = GetString(r, "ControlId"),
                        Payload = GetString(r, "Payload")
                    });
                }
            }

            list.Sort(delegate(OutboundMessage a, OutboundMessage b) { return a.MessageId.CompareTo(b.MessageId); });
            return list;
        }

        public void Complete(int messageId, MessageStatus status, string ackPayload, string error, int? retryMinutes)
        {
            DbCommand cmd = Proc("dbo.usp_Interface_Complete");
            In(cmd, "@MessageId", DbType.Int32, messageId);
            In(cmd, "@Status", DbType.Byte, (byte)status);
            In(cmd, "@AckPayload", DbType.String, ackPayload);
            In(cmd, "@Error", DbType.String, Truncate(error, 1000));
            In(cmd, "@RetryMinutes", DbType.Int32, retryMinutes);
            Db.ExecuteNonQuery(cmd);
        }

        public int LogInbound(string partner, string messageType, string controlId, string payload, MessageStatus status, int? patientId, string ackPayload, string error)
        {
            DbCommand cmd = Proc("dbo.usp_Interface_LogInbound");
            In(cmd, "@Partner", DbType.AnsiString, partner);
            In(cmd, "@MessageType", DbType.AnsiString, Truncate(messageType ?? "UNKNOWN", 10));
            In(cmd, "@ControlId", DbType.AnsiString, Truncate(controlId ?? string.Empty, 30));
            In(cmd, "@Payload", DbType.String, payload);
            In(cmd, "@Status", DbType.Byte, (byte)status);
            In(cmd, "@PatientId", DbType.Int32, patientId);
            In(cmd, "@AckPayload", DbType.String, ackPayload);
            In(cmd, "@Error", DbType.String, Truncate(error, 1000));
            Out(cmd, "@MessageId", DbType.Int32, 4);
            Db.ExecuteNonQuery(cmd);
            return OutValue<int>(cmd, "@MessageId");
        }

        public bool IsDuplicate(string partner, string controlId)
        {
            DbCommand cmd = Proc("dbo.usp_Interface_IsDuplicate");
            In(cmd, "@Partner", DbType.AnsiString, partner);
            In(cmd, "@ControlId", DbType.AnsiString, controlId);
            return Convert.ToInt32(Db.ExecuteScalar(cmd)) > 0;
        }

        public List<MessageLogRow> Search(string partner, MessageStatus? status, DateTime from, DateTime to, int maximumRows)
        {
            DbCommand cmd = Proc("dbo.usp_Interface_Search");
            In(cmd, "@Partner", DbType.AnsiString, string.IsNullOrEmpty(partner) ? null : partner);
            In(cmd, "@Status", DbType.Byte, status.HasValue ? (object)(byte)status.Value : null);
            In(cmd, "@From", DbType.DateTime, from);
            In(cmd, "@To", DbType.DateTime, to);
            In(cmd, "@MaximumRows", DbType.Int32, maximumRows);
            var list = new List<MessageLogRow>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new MessageLogRow
                    {
                        MessageId = GetInt(r, "MessageId"),
                        Direction = Convert.ToByte(r["Direction"]),
                        Partner = GetString(r, "Partner"),
                        MessageType = GetString(r, "MessageType"),
                        ControlId = GetString(r, "ControlId"),
                        Status = Convert.ToByte(r["Status"]),
                        Attempts = GetInt(r, "Attempts"),
                        Error = GetString(r, "Error"),
                        CreatedOn = GetDate(r, "CreatedOn"),
                        ProcessedOn = GetNullableDate(r, "ProcessedOn"),
                        PatientId = GetNullableInt(r, "PatientId"),
                        Mrn = GetString(r, "Mrn"),
                        LastName = GetString(r, "LastName")
                    });
                }
            }

            return list;
        }

        public string GetPayload(int messageId, out string ackPayload)
        {
            DbCommand cmd = Db.GetSqlStringCommand("SELECT Payload, AckPayload FROM dbo.InterfaceMessage WHERE MessageId = @MessageId");
            In(cmd, "@MessageId", DbType.Int32, messageId);
            ackPayload = null;
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                if (!r.Read())
                {
                    return null;
                }

                ackPayload = GetString(r, "AckPayload");
                return GetString(r, "Payload");
            }
        }

        public void Resend(int messageId, string userName)
        {
            DbCommand cmd = Proc("dbo.usp_Interface_Resend");
            In(cmd, "@MessageId", DbType.Int32, messageId);
            In(cmd, "@UserName", DbType.String, userName);
            Db.ExecuteNonQuery(cmd);
        }

        public PatientUpsertResult UpsertPatient(Hl7Patient patient, bool createIfMissing)
        {
            DbCommand cmd = Proc("dbo.usp_Hl7_UpsertPatient");
            In(cmd, "@Mrn", DbType.AnsiStringFixedLength, MedicalRecordNumber.IsValid(patient.Mrn) ? MedicalRecordNumber.Normalize(patient.Mrn) : null);
            In(cmd, "@HospitalId", DbType.AnsiString, Empty(patient.ExternalId));
            In(cmd, "@FirstName", DbType.String, patient.FirstName);
            In(cmd, "@MiddleName", DbType.String, Empty(patient.MiddleName));
            In(cmd, "@LastName", DbType.String, patient.LastName);
            In(cmd, "@DateOfBirth", DbType.Date, patient.DateOfBirth.Value.Date);
            In(cmd, "@Sex", DbType.Byte, (byte)PatientIdentity.ParseSex(patient.Sex));
            In(cmd, "@Address1", DbType.String, Empty(patient.Address1));
            In(cmd, "@Address2", DbType.String, Empty(patient.Address2));
            In(cmd, "@City", DbType.String, Empty(patient.City));
            In(cmd, "@State", DbType.AnsiStringFixedLength, Empty(patient.State));
            In(cmd, "@Zip", DbType.AnsiString, Empty(patient.Zip));
            In(cmd, "@HomePhone", DbType.AnsiString, PhoneNumber.Digits(patient.HomePhone));
            In(cmd, "@CreateIfMissing", DbType.Boolean, createIfMissing);
            Out(cmd, "@PatientId", DbType.Int32, 4);
            Out(cmd, "@Action", DbType.AnsiString, 10);
            Db.ExecuteNonQuery(cmd);
            return new PatientUpsertResult { PatientId = OutValue<int>(cmd, "@PatientId"), Action = OutValue<string>(cmd, "@Action") };
        }

        public void InsertLabResult(int patientId, Hl7Result result, Hl7Observation observation, int? messageId)
        {
            DbCommand cmd = Proc("dbo.usp_LabResult_Insert");
            In(cmd, "@PatientId", DbType.Int32, patientId);
            In(cmd, "@OrderNumber", DbType.AnsiString, Empty(result.PlacerOrderNumber));
            In(cmd, "@FillerOrderNumber", DbType.AnsiString, Empty(result.FillerOrderNumber));
            In(cmd, "@TestCode", DbType.AnsiString, result.TestCode);
            In(cmd, "@ObservationCode", DbType.AnsiString, observation.Code);
            In(cmd, "@ObservationName", DbType.String, Truncate(observation.Name, 100));
            In(cmd, "@Value", DbType.String, Truncate(observation.Value, 200));
            In(cmd, "@Units", DbType.AnsiString, Truncate(observation.Units, 20));
            In(cmd, "@ReferenceRange", DbType.AnsiString, Truncate(observation.ReferenceRange, 40));
            In(cmd, "@AbnormalFlag", DbType.AnsiString, Truncate(observation.AbnormalFlag, 4));
            In(cmd, "@ResultStatus", DbType.AnsiStringFixedLength, string.IsNullOrEmpty(observation.ResultStatus) ? "F" : observation.ResultStatus.Substring(0, 1));
            In(cmd, "@ObservedOn", DbType.DateTime, observation.ObservedAt ?? result.ObservedAt);
            In(cmd, "@MessageId", DbType.Int32, messageId);
            Db.ExecuteNonQuery(cmd);
        }

        public void MarkLabOrderSent(string orderNumber)
        {
            DbCommand cmd = Db.GetSqlStringCommand("UPDATE dbo.LabOrder SET SentOn = GETDATE(), Status = 'Sent' WHERE OrderNumber = @OrderNumber AND SentOn IS NULL");
            In(cmd, "@OrderNumber", DbType.AnsiString, orderNumber);
            Db.ExecuteNonQuery(cmd);
        }

        private static string Empty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static string Truncate(string value, int length)
        {
            return value != null && value.Length > length ? value.Substring(0, length) : value;
        }
    }
}
