using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Southridge.Clinic.Data.Repositories
{
    public class PatientRepository : RepositoryBase
    {
        public List<PatientSearchRow> Search(PatientSearchCriteria criteria, string sortExpression, int startRowIndex, int maximumRows, out int totalRows)
        {
            DbCommand cmd = Proc("dbo.usp_Patient_Search");
            In(cmd, "@LastName", DbType.String, Blank(criteria.LastName));
            In(cmd, "@FirstName", DbType.String, Blank(criteria.FirstName));
            In(cmd, "@DateOfBirth", DbType.Date, criteria.DateOfBirth);
            In(cmd, "@Mrn", DbType.AnsiStringFixedLength, Blank(criteria.Mrn));
            In(cmd, "@Phone", DbType.AnsiString, Blank(criteria.Phone));
            In(cmd, "@SortExpression", DbType.AnsiString, Blank(sortExpression));
            In(cmd, "@StartRowIndex", DbType.Int32, startRowIndex);
            In(cmd, "@MaximumRows", DbType.Int32, maximumRows);
            Out(cmd, "@TotalRows", DbType.Int32, 4);

            var rows = new List<PatientSearchRow>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    rows.Add(new PatientSearchRow
                    {
                        PatientId = GetInt(r, "PatientId"),
                        Mrn = GetString(r, "Mrn"),
                        FirstName = GetString(r, "FirstName"),
                        LastName = GetString(r, "LastName"),
                        DateOfBirth = GetDate(r, "DateOfBirth"),
                        Sex = Convert.ToByte(r["Sex"]),
                        HomePhone = GetString(r, "HomePhone"),
                        MobilePhone = GetString(r, "MobilePhone"),
                        City = GetString(r, "City"),
                        Balance = GetDecimal(r, "Balance"),
                        PreferredSite = GetString(r, "PreferredSite"),
                        LastVisit = GetNullableDate(r, "LastVisit"),
                        NextAppointment = GetNullableDate(r, "NextAppointment")
                    });
                }
            }

            totalRows = OutValue<int>(cmd, "@TotalRows");
            return rows;
        }

        public int Create(NewPatient patient, string createdBy, out string mrn)
        {
            DbCommand cmd = Proc("dbo.usp_Patient_Create");
            In(cmd, "@FirstName", DbType.String, patient.FirstName);
            In(cmd, "@MiddleName", DbType.String, Blank(patient.MiddleName));
            In(cmd, "@LastName", DbType.String, patient.LastName);
            In(cmd, "@DateOfBirth", DbType.Date, patient.DateOfBirth);
            In(cmd, "@Sex", DbType.Byte, patient.Sex);
            In(cmd, "@Address1", DbType.String, Blank(patient.Address1));
            In(cmd, "@Address2", DbType.String, Blank(patient.Address2));
            In(cmd, "@City", DbType.String, Blank(patient.City));
            In(cmd, "@State", DbType.AnsiStringFixedLength, Blank(patient.State));
            In(cmd, "@Zip", DbType.AnsiString, Blank(patient.Zip));
            In(cmd, "@HomePhone", DbType.AnsiString, Blank(patient.HomePhone));
            In(cmd, "@MobilePhone", DbType.AnsiString, Blank(patient.MobilePhone));
            In(cmd, "@Email", DbType.String, Blank(patient.Email));
            In(cmd, "@PreferredSiteId", DbType.Int32, patient.PreferredSiteId);
            In(cmd, "@HospitalId", DbType.AnsiString, Blank(patient.HospitalId));
            In(cmd, "@PortalUserName", DbType.String, Blank(patient.PortalUserName));
            In(cmd, "@CreatedBy", DbType.String, createdBy);
            Out(cmd, "@PatientId", DbType.Int32, 4);
            Out(cmd, "@Mrn", DbType.AnsiStringFixedLength, 9);
            Db.ExecuteNonQuery(cmd);
            mrn = OutValue<string>(cmd, "@Mrn");
            return OutValue<int>(cmd, "@PatientId");
        }

        public List<PatientSearchRow> FindDuplicates(string lastName, DateTime dateOfBirth)
        {
            DbCommand cmd = Proc("dbo.usp_Patient_FindDuplicates");
            In(cmd, "@LastName", DbType.String, lastName);
            In(cmd, "@DateOfBirth", DbType.Date, dateOfBirth);
            var rows = new List<PatientSearchRow>();
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    rows.Add(new PatientSearchRow
                    {
                        PatientId = GetInt(r, "PatientId"),
                        Mrn = GetString(r, "Mrn"),
                        FirstName = GetString(r, "FirstName"),
                        LastName = GetString(r, "LastName"),
                        DateOfBirth = GetDate(r, "DateOfBirth"),
                        HomePhone = GetString(r, "HomePhone"),
                        MobilePhone = GetString(r, "MobilePhone")
                    });
                }
            }

            return rows;
        }

        /// <summary>Connects a booking site account to an existing chart. Returns the patient id or null when nothing matched.</summary>
        public int? LinkPortalAccount(string mrn, string lastName, DateTime dateOfBirth, string userName)
        {
            DbCommand cmd = Proc("dbo.usp_Patient_LinkPortalAccount");
            In(cmd, "@Mrn", DbType.AnsiStringFixedLength, mrn);
            In(cmd, "@LastName", DbType.String, lastName);
            In(cmd, "@DateOfBirth", DbType.Date, dateOfBirth);
            In(cmd, "@PortalUserName", DbType.String, userName);
            Out(cmd, "@PatientId", DbType.Int32, 4);
            Db.ExecuteNonQuery(cmd);
            object value = Db.GetParameterValue(cmd, "@PatientId");
            return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        public StatementData GetStatementData(int patientId, DateTime fromDate)
        {
            DbCommand cmd = Proc("dbo.usp_Patient_Statement");
            In(cmd, "@PatientId", DbType.Int32, patientId);
            In(cmd, "@FromDate", DbType.Date, fromDate);
            StatementData data = null;
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                if (r.Read())
                {
                    data = new StatementData
                    {
                        PatientId = GetInt(r, "PatientId"),
                        Mrn = GetString(r, "Mrn"),
                        FirstName = GetString(r, "FirstName"),
                        LastName = GetString(r, "LastName"),
                        Address1 = GetString(r, "Address1"),
                        Address2 = GetString(r, "Address2"),
                        City = GetString(r, "City"),
                        State = GetString(r, "State"),
                        Zip = GetString(r, "Zip"),
                        Balance = GetDecimal(r, "Balance"),
                        PreviousAmountDue = GetDecimal(r, "PreviousAmountDue"),
                        PreviousDunningLevel = GetInt(r, "PreviousDunningLevel")
                    };
                }

                if (data == null)
                {
                    return null;
                }

                r.NextResult();
                while (r.Read())
                {
                    data.Lines.Add(new StatementLine
                    {
                        DateOfService = GetDate(r, "DateOfService"),
                        CptCode = GetString(r, "CptCode"),
                        Description = GetString(r, "Description"),
                        Units = GetInt(r, "Units"),
                        Amount = GetDecimal(r, "Amount"),
                        InsurancePaid = GetDecimal(r, "PaidAmount"),
                        Adjustment = GetDecimal(r, "AdjustmentAmount"),
                        PatientAmount = GetDecimal(r, "PatientAmount"),
                        ProviderName = GetString(r, "ProviderLastName"),
                        SiteName = GetString(r, "SiteName")
                    });
                }

                r.NextResult();
                while (r.Read())
                {
                    data.Payments.Add(new StatementPayment
                    {
                        PostedOn = GetDate(r, "PostedOn"),
                        Source = GetString(r, "Source"),
                        Method = GetString(r, "Method"),
                        Amount = GetDecimal(r, "Amount"),
                        Reference = GetString(r, "Reference")
                    });
                }
            }

            return data;
        }

        public void PostPatientPayment(int patientId, decimal amount, string method, string reference, string postedBy)
        {
            DbCommand cmd = Proc("dbo.usp_Payment_PostPatient");
            In(cmd, "@PatientId", DbType.Int32, patientId);
            In(cmd, "@Amount", DbType.Currency, amount);
            In(cmd, "@Method", DbType.AnsiString, method);
            In(cmd, "@Reference", DbType.AnsiString, Blank(reference));
            In(cmd, "@PostedBy", DbType.String, postedBy);
            Db.ExecuteNonQuery(cmd);
        }

        private static string Blank(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value.Trim();
        }
    }
}
