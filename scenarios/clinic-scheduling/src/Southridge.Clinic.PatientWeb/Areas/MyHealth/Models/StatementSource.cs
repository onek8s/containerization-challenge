using System;
using System.Collections.Generic;
using Southridge.Clinic.Billing;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;

namespace Southridge.Clinic.PatientWeb.Areas.MyHealth.Models
{
    public interface IStatementSource
    {
        IList<StatementRow> Recent(int patientId);
        IList<PatientClaimRow> Claims(int patientId, DateTime fromDate);
        StatementResult Current(int patientId);
    }

    /// <summary>Statements and claims straight from the clinic database (the booking web service has no billing operations).</summary>
    public class StatementSource : IStatementSource
    {
        private readonly StatementService _statements;

        public StatementSource(StatementService statements)
        {
            _statements = statements;
        }

        public IList<StatementRow> Recent(int patientId)
        {
            return new BillingRepository().StatementsForPatient(patientId, 12);
        }

        public IList<PatientClaimRow> Claims(int patientId, DateTime fromDate)
        {
            return new BillingRepository().ClaimsForPatient(patientId, fromDate, DateTime.Today);
        }

        public StatementResult Current(int patientId)
        {
            return _statements.Preview(patientId, DateTime.Today);
        }
    }
}
