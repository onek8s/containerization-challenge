using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.ServiceModel;
using System.ServiceModel.Activation;
using Southridge.Clinic.Contracts;
using Southridge.Clinic.Data;
using Southridge.Clinic.Documents;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Referrals;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.Services
{
    /// <summary>
    /// Records exchange with the hospital and the referring practice network. Callers authenticate with a
    /// ServiceAccount user from the clinic membership database (wsHttpBinding message security).
    /// </summary>
    [ServiceBehavior(Namespace = Namespaces.Records, InstanceContextMode = InstanceContextMode.PerCall)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class RecordsService : IRecordsService
    {
        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public PatientRecord GetPatient(string mrn)
        {
            return ServiceCall.Run("GetPatient", delegate
            {
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    string normalized = MedicalRecordNumber.Normalize(mrn);
                    Patient p = db.Patients.SingleOrDefault(x => x.Mrn == normalized);
                    if (p == null)
                    {
                        throw new NotFoundException("Patient " + mrn + " was not found.");
                    }

                    Audit("GetPatient", p.PatientId, p.Mrn);
                    var allergies = db.IntakeForms.Where(f => f.PatientId == p.PatientId && f.Allergies != null)
                        .OrderByDescending(f => f.SubmittedOn).Select(f => f.Allergies).FirstOrDefault();
                    return new PatientRecord
                    {
                        Mrn = p.Mrn,
                        FirstName = p.FirstName,
                        MiddleName = p.MiddleName,
                        LastName = p.LastName,
                        DateOfBirth = p.DateOfBirth,
                        Sex = ((Sex)p.Sex).ToString(),
                        Address1 = p.Address1,
                        City = p.City,
                        State = p.State,
                        Zip = p.Zip,
                        Phone = PhoneNumber.Format(p.MobilePhone ?? p.HomePhone),
                        PrimaryProvider = p.PrimaryProvider == null ? null : ClinicLookups.ProviderName(p.PrimaryProvider),
                        Allergies = allergies,
                        Coverage = p.PatientInsurances.OrderBy(i => i.Sequence).Select(i => new CoverageInfo
                        {
                            Sequence = i.Sequence,
                            PayerName = i.Payer.Name,
                            MemberId = i.MemberId,
                            GroupNumber = i.GroupNumber,
                            EffectiveFrom = i.EffectiveFrom,
                            EffectiveTo = i.EffectiveTo
                        }).ToList()
                    };
                }
            });
        }

        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public List<PatientMatch> FindPatients(string lastName, DateTime dateOfBirth)
        {
            return ServiceCall.Run("FindPatients", delegate
            {
                if (string.IsNullOrEmpty(lastName) || lastName.Trim().Length < 2)
                {
                    throw new ClinicRuleException("At least two letters of the last name are required.");
                }

                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    string prefix = lastName.Trim();
                    return db.Patients
                        .Where(p => p.IsActive && p.DateOfBirth == dateOfBirth.Date && p.LastName.StartsWith(prefix))
                        .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                        .Take(20)
                        .Select(p => new PatientMatch { Mrn = p.Mrn, Name = p.LastName + ", " + p.FirstName, DateOfBirth = p.DateOfBirth, City = p.City })
                        .ToList();
                }
            });
        }

        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public List<VisitInfo> GetVisits(string mrn, DateTime from, DateTime to)
        {
            return ServiceCall.Run("GetVisits", delegate
            {
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    string normalized = MedicalRecordNumber.Normalize(mrn);
                    var encounters = db.Encounters
                        .Where(e => e.Patient.Mrn == normalized && e.DateOfService >= from.Date && e.DateOfService <= to.Date && e.Status != (byte)EncounterStatus.Voided)
                        .OrderByDescending(e => e.DateOfService)
                        .ToList();
                    if (encounters.Count > 0)
                    {
                        Audit("GetVisits", encounters[0].PatientId, normalized);
                    }

                    return encounters.Select(e => new VisitInfo
                    {
                        EncounterId = e.EncounterId,
                        DateOfService = e.DateOfService,
                        ProviderName = ClinicLookups.ProviderName(e.Provider),
                        SiteName = e.Site.Name,
                        Diagnoses = e.EncounterDiagnosis.OrderBy(d => d.Sequence).Select(d => d.Code).ToList(),
                        Procedures = e.Charges.Where(c => c.Status != (byte)ChargeStatus.Voided).OrderBy(c => c.LineNumber).Select(c => c.CptCode).ToList()
                    }).ToList();
                }
            });
        }

        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public byte[] GetVisitSummaryPdf(int encounterId)
        {
            return ServiceCall.Run("GetVisitSummaryPdf", delegate
            {
                byte[] pdf = VisitSummaryBuilder.Pdf(encounterId);
                if (pdf == null)
                {
                    throw new NotFoundException("Encounter " + encounterId + " was not found.");
                }

                Audit("GetVisitSummaryPdf", null, encounterId.ToString());
                return pdf;
            });
        }

        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public List<LabResultInfo> GetLabResults(string mrn, DateTime from)
        {
            return ServiceCall.Run("GetLabResults", delegate
            {
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    string normalized = MedicalRecordNumber.Normalize(mrn);
                    return db.LabResults
                        .Where(r => r.Patient.Mrn == normalized && r.ReceivedOn >= from)
                        .OrderByDescending(r => r.ObservedOn).ThenBy(r => r.ObservationName)
                        .Select(r => new LabResultInfo
                        {
                            OrderNumber = r.OrderNumber,
                            TestCode = r.ObservationCode,
                            TestName = r.ObservationName,
                            Value = r.Value,
                            Units = r.Units,
                            ReferenceRange = r.ReferenceRange,
                            AbnormalFlag = r.AbnormalFlag,
                            ObservedOn = r.ObservedOn ?? r.ReceivedOn
                        }).ToList();
                }
            });
        }

        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public ReferralReceipt SubmitReferral(ReferralSubmission submission)
        {
            return ServiceCall.Run("SubmitReferral", delegate
            {
                Patient patient = ClinicLookups.PatientByMrn(submission.Mrn);
                if (patient == null)
                {
                    throw new NotFoundException("Patient " + submission.Mrn + " was not found. Register the patient first.");
                }

                string caller = ServiceCall.CallerName;
                Referral referral = ReferralSessions.InUnitOfWork(session =>
                {
                    var service = new ReferralService(session, ClinicContainer.Resolve<IClock>());
                    Referral created = service.Receive(new NewReferral
                    {
                        PatientId = patient.PatientId,
                        ReferringProviderName = submission.ReferringProviderName,
                        ReferringProviderNpi = submission.ReferringProviderNpi,
                        ReferringPractice = submission.ReferringPractice,
                        ReferringPhone = submission.ReferringPhone,
                        SpecialtyCode = submission.SpecialtyCode,
                        Priority = (ReferralPriority)Math.Max(0, Math.Min(2, submission.Priority)),
                        Reason = submission.Reason,
                        DiagnosisCode = submission.DiagnosisCode,
                        Source = "Partner"
                    }, caller);
                    if (submission.Attachment != null && submission.Attachment.Length > 0)
                    {
                        created.AddDocument(string.IsNullOrEmpty(submission.AttachmentFileName) ? "referral.pdf" : submission.AttachmentFileName,
                            "application/pdf", submission.Attachment, caller, DateTime.Now);
                    }

                    return created;
                });

                ReferralWorkflowService.StartWorkflow(referral.Id, (int)referral.Priority);
                return new ReferralReceipt { ReferralNumber = referral.ReferralNumber, DueBy = referral.DueBy };
            });
        }

        [PrincipalPermission(SecurityAction.Demand, Role = ClinicRoles.ServiceAccount)]
        public ReferralStatusInfo GetReferralStatus(string referralNumber)
        {
            return ServiceCall.Run("GetReferralStatus", delegate
            {
                return ReferralSessions.InUnitOfWork(session =>
                {
                    Referral r = new ReferralRepository(session).GetByNumber(referralNumber);
                    if (r == null)
                    {
                        throw new NotFoundException("Referral " + referralNumber + " was not found.");
                    }

                    DateTime? start = null;
                    if (r.AppointmentId.HasValue)
                    {
                        using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                        {
                            start = db.Appointments.Where(a => a.AppointmentId == r.AppointmentId.Value).Select(a => (DateTime?)a.StartsAt).SingleOrDefault();
                        }
                    }

                    return new ReferralStatusInfo
                    {
                        ReferralNumber = r.ReferralNumber,
                        Status = r.Status.ToString(),
                        ReceivedOn = r.ReceivedOn,
                        DecidedOn = r.DecidedOn,
                        DecisionNote = r.DecisionNote,
                        AppointmentStart = start
                    };
                });
            });
        }

        private static void Audit(string action, int? patientId, string entityId)
        {
            new Southridge.Clinic.Data.Repositories.SystemRepository().Audit(ServiceCall.CallerName, "Records." + action, "Patient", entityId, patientId, null,
                OperationContext.Current == null ? null : RemoteAddress());
        }

        private static string RemoteAddress()
        {
            object property;
            if (OperationContext.Current.IncomingMessageProperties.TryGetValue(System.ServiceModel.Channels.RemoteEndpointMessageProperty.Name, out property))
            {
                return ((System.ServiceModel.Channels.RemoteEndpointMessageProperty)property).Address;
            }

            return null;
        }
    }
}
