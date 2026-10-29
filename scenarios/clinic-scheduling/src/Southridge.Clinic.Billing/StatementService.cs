using System;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Documents;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Billing
{
    public class StatementResult
    {
        public int StatementId { get; set; }
        public decimal AmountDue { get; set; }
        public int DunningLevel { get; set; }
        public DateTime DueDate { get; set; }
        public byte[] Pdf { get; set; }
    }

    /// <summary>Builds patient statements. The nightly batch records them; the portal and services only preview.</summary>
    public class StatementService
    {
        private readonly PatientRepository _patients;
        private readonly BillingRepository _billing;
        private readonly StatementPolicy _policy;
        private readonly int _historyDays;

        public StatementService()
            : this(new PatientRepository(), new BillingRepository(), CreatePolicy(new SystemRepository()), ClinicSettings.GetInt("StatementHistoryDays", 120))
        {
        }

        public StatementService(PatientRepository patients, BillingRepository billing, StatementPolicy policy, int historyDays)
        {
            _patients = patients;
            _billing = billing;
            _policy = policy;
            _historyDays = historyDays;
        }

        /// <summary>Statement terms are maintained by the billing office in Admin, settings (dbo.AppSetting).</summary>
        public static StatementPolicy CreatePolicy(SystemRepository settings)
        {
            return new StatementPolicy(settings.GetSetting("Billing.StatementDueDays", 21), settings.GetSetting("Billing.CollectionsDunningLevel", 3));
        }

        public StatementResult Preview(int patientId, DateTime statementDate)
        {
            StatementData data = _patients.GetStatementData(patientId, statementDate.AddDays(-_historyDays));
            if (data == null)
            {
                return null;
            }

            int level = _policy.NextDunningLevel(data);
            DateTime due = _policy.DueDate(statementDate);
            decimal amountDue = Math.Max(0m, data.Balance);
            return new StatementResult
            {
                AmountDue = amountDue,
                DunningLevel = level,
                DueDate = due,
                Pdf = PdfDocuments.Statement(data, statementDate, due, amountDue, _policy.Message(level))
            };
        }

        public StatementResult Issue(int patientId, DateTime statementDate, string fileName)
        {
            StatementData data = _patients.GetStatementData(patientId, statementDate.AddDays(-_historyDays));
            if (data == null)
            {
                return null;
            }

            int level = _policy.NextDunningLevel(data);
            DateTime due = _policy.DueDate(statementDate);
            decimal charges = 0m, payments = 0m;
            foreach (StatementLine l in data.Lines)
            {
                charges += l.PatientAmount;
            }

            foreach (StatementPayment p in data.Payments)
            {
                payments += p.Amount;
            }

            decimal amountDue = Math.Max(0m, data.Balance);
            decimal previous = amountDue - charges + payments;
            byte[] pdf = PdfDocuments.Statement(data, statementDate, due, amountDue, _policy.Message(level));
            int id = _billing.InsertStatement(patientId, statementDate, previous, charges, payments, amountDue, due, fileName, level);
            return new StatementResult { StatementId = id, AmountDue = amountDue, DunningLevel = level, DueDate = due, Pdf = pdf };
        }
    }
}
