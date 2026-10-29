using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.ServiceModel;
using System.ServiceModel.Activation;
using Southridge.Clinic.Billing;
using Southridge.Clinic.Contracts;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.Services
{
    [ServiceBehavior(Namespace = Namespaces.Billing, InstanceContextMode = InstanceContextMode.PerCall)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class BillingService : IBillingService
    {
        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public AccountSummary GetAccount(string mrn)
        {
            return ServiceCall.Run("GetAccount", delegate
            {
                Patient p = Find(mrn);
                StatementRow last = new BillingRepository().StatementsForPatient(p.PatientId, 1).FirstOrDefault();
                return new AccountSummary
                {
                    Mrn = p.Mrn,
                    Name = p.LastName + ", " + p.FirstName,
                    Balance = p.Balance,
                    LastStatementDate = last == null ? (DateTime?)null : last.StatementDate,
                    LastStatementAmount = last == null ? (decimal?)null : last.AmountDue,
                    DunningLevel = last == null ? 0 : last.DunningLevel
                };
            });
        }

        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public List<ClaimInfo> GetClaims(string mrn, DateTime from, DateTime to)
        {
            return ServiceCall.Run("GetClaims", delegate
            {
                Patient p = Find(mrn);
                return new BillingRepository().ClaimsForPatient(p.PatientId, from, to).Select(c => new ClaimInfo
                {
                    ClaimNumber = c.ClaimNumber,
                    DateOfService = c.DateOfService,
                    PayerName = c.PayerName,
                    Status = c.StatusText,
                    Charged = c.TotalCharge,
                    Paid = c.PaidAmount,
                    Adjusted = c.AdjustmentAmount,
                    PatientResponsibility = c.PatientResponsibility
                }).ToList();
            });
        }

        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public PaymentReceipt PostPayment(PatientPayment payment)
        {
            return ServiceCall.Run("PostPayment", delegate
            {
                if (payment.Amount <= 0 || payment.Amount > 10000m)
                {
                    throw new ClinicRuleException("Payment amount must be between 0.01 and 10,000.00.");
                }

                string method = (payment.Method ?? string.Empty).Trim().ToUpperInvariant();
                if (method != "CARD" && method != "CHECK" && method != "CASH" && method != "ACH")
                {
                    throw new ClinicRuleException("Unknown payment method " + payment.Method + ".");
                }

                Patient p = Find(payment.Mrn);
                new PatientRepository().PostPatientPayment(p.PatientId, payment.Amount, method, payment.Reference, ServiceCall.CallerName);
                ClinicLog.Audit(ServiceCall.CallerName, "PostPayment", p.Mrn + " " + payment.Amount.ToString("0.00"));
                Patient after = Find(payment.Mrn);
                return new PaymentReceipt { Mrn = after.Mrn, Amount = payment.Amount, NewBalance = after.Balance, PostedOn = DateTime.Now };
            });
        }

        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public byte[] GetStatementPdf(string mrn)
        {
            return ServiceCall.Run("GetStatementPdf", delegate
            {
                Patient p = Find(mrn);
                StatementResult result = new StatementService().Preview(p.PatientId, DateTime.Today);
                if (result == null)
                {
                    throw new NotFoundException("No statement data for " + mrn + ".");
                }

                return result.Pdf;
            });
        }

        private static Patient Find(string mrn)
        {
            Patient p = ClinicLookups.PatientByMrn(mrn);
            if (p == null)
            {
                throw new NotFoundException("Patient " + mrn + " was not found.");
            }

            return p;
        }
    }
}
