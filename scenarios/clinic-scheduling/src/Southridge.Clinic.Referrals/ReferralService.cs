using System;
using System.Collections.Generic;
using NHibernate;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Referrals
{
    public class NewReferral
    {
        public int PatientId { get; set; }
        public string ReferringProviderName { get; set; }
        public string ReferringProviderNpi { get; set; }
        public string ReferringPractice { get; set; }
        public string ReferringPhone { get; set; }
        public string SpecialtyCode { get; set; }
        public int? RequestedProviderId { get; set; }
        public ReferralPriority Priority { get; set; }
        public string Reason { get; set; }
        public string DiagnosisCode { get; set; }
        public string Source { get; set; }
    }

    /// <summary>Referral state changes. Each call runs inside the caller's NHibernate session and transaction.</summary>
    public class ReferralService
    {
        private static readonly IDictionary<ReferralPriority, int> ReviewHours = new Dictionary<ReferralPriority, int>
        {
            { ReferralPriority.Routine, 72 },
            { ReferralPriority.Urgent, 24 },
            { ReferralPriority.Stat, 4 }
        };

        private readonly ReferralRepository _repository;
        private readonly IClock _clock;

        public ReferralService(ISession session, IClock clock)
        {
            _repository = new ReferralRepository(session);
            _clock = clock;
        }

        public ReferralRepository Repository
        {
            get { return _repository; }
        }

        public Referral Receive(NewReferral request, string user)
        {
            Validate(request);
            ReferralPatient patient = _repository.GetPatient(request.PatientId);
            if (patient == null)
            {
                throw new ClinicRuleException("Patient " + request.PatientId + " does not exist.");
            }

            DateTime now = _clock.Now;
            var referral = new Referral
            {
                ReferralNumber = _repository.NextReferralNumber(),
                Patient = patient,
                ReferringProviderName = request.ReferringProviderName.Trim(),
                ReferringProviderNpi = request.ReferringProviderNpi,
                ReferringPractice = request.ReferringPractice,
                ReferringPhone = PhoneNumber.Digits(request.ReferringPhone),
                SpecialtyCode = request.SpecialtyCode,
                RequestedProviderId = request.RequestedProviderId,
                Priority = request.Priority,
                Reason = request.Reason.Trim(),
                DiagnosisCode = string.IsNullOrEmpty(request.DiagnosisCode) ? null : request.DiagnosisCode.Trim().ToUpperInvariant(),
                Source = string.IsNullOrEmpty(request.Source) ? "Internal" : request.Source,
                ReceivedOn = now,
                DueBy = now.AddHours(ReviewHours[request.Priority])
            };
            referral.Status = ReferralStatus.Received;
            referral.AddNote(user, "Referral received (" + referral.Source + ").", now);
            _repository.Save(referral);
            ClinicLog.Audit(user, "ReferralReceived", referral.ReferralNumber);
            return referral;
        }

        public void StartReview(Referral referral, string user)
        {
            Ensure(referral, ReferralStatus.Received);
            referral.Status = ReferralStatus.InReview;
            referral.AddNote(user, "Review started.", _clock.Now);
        }

        public void Approve(Referral referral, string user, string authorizationNumber, int? visits, string note)
        {
            Ensure(referral, ReferralStatus.Received, ReferralStatus.InReview);
            Decide(referral, ReferralStatus.Approved, user, note);
            referral.AuthorizationNumber = string.IsNullOrEmpty(authorizationNumber) ? null : authorizationNumber.Trim();
            referral.VisitsAuthorized = visits;
            referral.ExpiresOn = _clock.Now.Date.AddDays(180);
        }

        public void Decline(Referral referral, string user, string note)
        {
            if (string.IsNullOrEmpty(note))
            {
                throw new ClinicRuleException("A reason is required to decline a referral.");
            }

            Ensure(referral, ReferralStatus.Received, ReferralStatus.InReview);
            Decide(referral, ReferralStatus.Declined, user, note);
        }

        public void LinkAppointment(Referral referral, int appointmentId, string user)
        {
            Ensure(referral, ReferralStatus.Approved);
            referral.AppointmentId = appointmentId;
            referral.Status = ReferralStatus.Scheduled;
            referral.AddNote(user, "Appointment " + appointmentId + " booked.", _clock.Now);
        }

        public void Expire(Referral referral, string reason)
        {
            if (!referral.IsOpen)
            {
                return;
            }

            referral.Status = ReferralStatus.Expired;
            referral.AddNote("system", reason, _clock.Now);
        }

        public void Cancel(Referral referral, string user, string reason)
        {
            if (!referral.IsOpen)
            {
                throw new ClinicRuleException("Referral " + referral.ReferralNumber + " is already closed.");
            }

            referral.Status = ReferralStatus.Cancelled;
            referral.AddNote(user, "Cancelled: " + reason, _clock.Now);
        }

        private void Decide(Referral referral, ReferralStatus status, string user, string note)
        {
            DateTime now = _clock.Now;
            referral.Status = status;
            referral.DecidedBy = user;
            referral.DecidedOn = now;
            referral.DecisionNote = note;
            referral.AddNote(user, status + (string.IsNullOrEmpty(note) ? "." : ": " + note), now);
            ClinicLog.Audit(user, "Referral" + status, referral.ReferralNumber);
        }

        private static void Ensure(Referral referral, params ReferralStatus[] allowed)
        {
            if (Array.IndexOf(allowed, referral.Status) < 0)
            {
                throw new ClinicRuleException("Referral " + referral.ReferralNumber + " is " + referral.Status + ".");
            }
        }

        private static void Validate(NewReferral request)
        {
            if (string.IsNullOrEmpty(request.ReferringProviderName))
            {
                throw new ClinicRuleException("Referring provider is required.");
            }

            if (string.IsNullOrEmpty(request.SpecialtyCode))
            {
                throw new ClinicRuleException("Specialty is required.");
            }

            if (string.IsNullOrEmpty(request.Reason))
            {
                throw new ClinicRuleException("Reason for referral is required.");
            }

            if (!string.IsNullOrEmpty(request.ReferringProviderNpi) && !Npi.IsValid(request.ReferringProviderNpi))
            {
                throw new ClinicRuleException("Referring NPI " + request.ReferringProviderNpi + " is not valid.");
            }
        }
    }
}
