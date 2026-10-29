using System;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Billing
{
    /// <summary>Charge amounts and expected payment (allowed amount) from the fee schedule.</summary>
    public class FeeCalculator
    {
        public decimal ChargeAmount(ClinicDataContext db, string cptCode)
        {
            ProcedureCode code = db.ProcedureCodes.SingleOrDefault(p => p.CptCode == cptCode);
            if (code == null || !code.IsActive)
            {
                throw new ClinicRuleException("Procedure code " + cptCode + " is not in the charge master.");
            }

            return code.StandardFee;
        }

        public decimal? AllowedAmount(ClinicDataContext db, int payerId, string cptCode, DateTime dateOfService)
        {
            FeeSchedule fee = db.FeeSchedules
                .Where(f => f.PayerId == payerId && f.CptCode == cptCode && f.EffectiveFrom <= dateOfService && (f.EffectiveTo == null || f.EffectiveTo >= dateOfService))
                .OrderByDescending(f => f.EffectiveFrom)
                .FirstOrDefault();
            return fee == null ? (decimal?)null : fee.AllowedAmount;
        }

        /// <summary>Estimated patient share at check-out: copay for office visits, otherwise the remainder above the allowed amount is written off.</summary>
        public static decimal EstimatePatientShare(decimal allowed, decimal copay, bool isOfficeVisit, PayerType payerType)
        {
            if (payerType == PayerType.SelfPay)
            {
                return Money.Round(allowed * 0.7m);
            }

            return isOfficeVisit ? Math.Min(copay, allowed) : 0m;
        }
    }
}
