using System;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Documents;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Referrals;
using Southridge.Clinic.Workflow;

namespace Southridge.Clinic.Services
{
    /// <summary>
    /// Host side of the referral workflow (IReferralActions). Runs on WF scheduler threads, so each call opens
    /// its own NHibernate session.
    /// </summary>
    public class ReferralActions : IReferralActions
    {
        private readonly IMailer _mailer;
        private readonly IClock _clock;

        public ReferralActions(IMailer mailer, IClock clock)
        {
            _mailer = mailer;
            _clock = clock;
        }

        public void Received(int referralId, Guid instanceId)
        {
            ReferralSessions.InUnitOfWork(session =>
            {
                Referral r = session.Get<Referral>(referralId);
                r.WorkflowInstanceId = instanceId;
                r.AddNote("workflow", "Approval workflow started; decision due " + r.DueBy.ToString("g") + ".", _clock.Now);
            });
        }

        public void Escalate(int referralId, int escalation, string reason)
        {
            string number = null;
            ReferralSessions.InUnitOfWork(session =>
            {
                Referral r = session.Get<Referral>(referralId);
                number = r.ReferralNumber;
                r.AddNote("workflow", reason, _clock.Now);
            });

            string to = ClinicSettings.Get("ReferralEscalationMail", "referrals@southridgemedical.example");
            try
            {
                _mailer.Send(to, "Referral " + number + " needs attention (" + escalation + ")",
                    reason + "\r\n\r\nOpen the referral queue in the clinic system to review it.", false);
            }
            catch (Exception ex)
            {
                ClinicLog.Error(LogCategory.General, "Escalation mail for referral " + number + " failed.", ex);
            }
        }

        public void SendDecisionLetter(int referralId, bool approved)
        {
            ReferralSessions.InUnitOfWork(session =>
            {
                Referral r = session.Get<Referral>(referralId);
                string specialty;
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    specialty = db.Specialties.Where(s => s.SpecialtyCode == r.SpecialtyCode).Select(s => s.Name).SingleOrDefault() ?? r.SpecialtyCode;
                }

                byte[] pdf = PdfDocuments.ReferralLetter(new ReferralLetterData
                {
                    ReferringProviderName = r.ReferringProviderName,
                    ReferringPractice = r.ReferringPractice,
                    ReferralNumber = r.ReferralNumber,
                    PatientName = r.Patient.FirstName + " " + r.Patient.LastName,
                    PatientDateOfBirth = r.Patient.DateOfBirth,
                    Specialty = specialty,
                    Approved = approved,
                    AuthorizationNumber = r.AuthorizationNumber,
                    VisitsAuthorized = r.VisitsAuthorized,
                    DecisionNote = r.DecisionNote,
                    DecidedBy = r.DecidedBy
                });
                r.AddDocument(r.ReferralNumber + (approved ? "-accepted" : "-declined") + ".pdf", "application/pdf", pdf, "workflow", _clock.Now);
                r.AddNote("workflow", "Decision letter generated for " + r.ReferringProviderName + " (fax queue).", _clock.Now);
            });
        }

        public void Expire(int referralId, string reason)
        {
            ReferralSessions.InUnitOfWork(session =>
            {
                Referral r = session.Get<Referral>(referralId);
                new ReferralService(session, _clock).Expire(r, reason);
            });
        }

        public void Completed(int referralId, string outcome)
        {
            ReferralSessions.InUnitOfWork(session =>
            {
                Referral r = session.Get<Referral>(referralId);
                r.AddNote("workflow", "Workflow finished: " + outcome + ".", _clock.Now);
            });
        }
    }
}
