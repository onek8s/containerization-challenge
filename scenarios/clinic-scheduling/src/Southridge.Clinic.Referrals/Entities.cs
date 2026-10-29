using System;
using System.Collections.Generic;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Referrals
{
    /// <summary>Incoming referral from an outside or internal provider. Mapped in Mappings/Referral.hbm.xml.</summary>
    public class Referral
    {
        private IList<ReferralNote> _notes = new List<ReferralNote>();
        private IList<ReferralDocument> _documents = new List<ReferralDocument>();

        public virtual int Id { get; protected set; }
        public virtual int Version { get; protected set; }
        public virtual string ReferralNumber { get; set; }
        public virtual ReferralPatient Patient { get; set; }
        public virtual string ReferringProviderName { get; set; }
        public virtual string ReferringProviderNpi { get; set; }
        public virtual string ReferringPractice { get; set; }
        public virtual string ReferringPhone { get; set; }
        public virtual string SpecialtyCode { get; set; }
        public virtual int? RequestedProviderId { get; set; }
        public virtual ReferralPriority Priority { get; set; }
        public virtual string Reason { get; set; }
        public virtual string DiagnosisCode { get; set; }
        public virtual ReferralStatus Status { get; protected internal set; }
        public virtual string Source { get; set; }
        public virtual string AuthorizationNumber { get; set; }
        public virtual int? VisitsAuthorized { get; set; }
        public virtual DateTime ReceivedOn { get; set; }
        public virtual DateTime DueBy { get; set; }
        public virtual string DecidedBy { get; protected internal set; }
        public virtual DateTime? DecidedOn { get; protected internal set; }
        public virtual string DecisionNote { get; protected internal set; }
        public virtual int? AppointmentId { get; protected internal set; }
        public virtual Guid? WorkflowInstanceId { get; set; }
        public virtual DateTime? ExpiresOn { get; set; }

        public virtual IList<ReferralNote> Notes
        {
            get { return _notes; }
            protected set { _notes = value; }
        }

        public virtual IList<ReferralDocument> Documents
        {
            get { return _documents; }
            protected set { _documents = value; }
        }

        public virtual bool IsOpen
        {
            get { return Status == ReferralStatus.Received || Status == ReferralStatus.InReview || Status == ReferralStatus.Approved; }
        }

        public virtual bool IsOverdue(DateTime now)
        {
            return (Status == ReferralStatus.Received || Status == ReferralStatus.InReview) && DueBy < now;
        }

        public virtual ReferralNote AddNote(string author, string text, DateTime now)
        {
            var note = new ReferralNote { Referral = this, Author = author, Text = text, CreatedOn = now };
            _notes.Add(note);
            return note;
        }

        public virtual ReferralDocument AddDocument(string fileName, string contentType, byte[] content, string user, DateTime now)
        {
            var doc = new ReferralDocument { Referral = this, FileName = fileName, ContentType = contentType, Content = content, UploadedBy = user, UploadedOn = now };
            _documents.Add(doc);
            return doc;
        }
    }

    public class ReferralNote
    {
        public virtual int Id { get; protected set; }
        public virtual Referral Referral { get; set; }
        public virtual string Author { get; set; }
        public virtual string Text { get; set; }
        public virtual DateTime CreatedOn { get; set; }
    }

    /// <summary>Scanned fax or uploaded letter. Content is a lazy property so lists do not pull the blobs.</summary>
    public class ReferralDocument
    {
        public virtual int Id { get; protected set; }
        public virtual Referral Referral { get; set; }
        public virtual string FileName { get; set; }
        public virtual string ContentType { get; set; }
        public virtual byte[] Content { get; set; }
        public virtual string UploadedBy { get; set; }
        public virtual DateTime UploadedOn { get; set; }
    }

    /// <summary>Read-only view of dbo.Patient for the referral screens.</summary>
    public class ReferralPatient
    {
        public virtual int Id { get; protected set; }
        public virtual string Mrn { get; protected set; }
        public virtual string FirstName { get; protected set; }
        public virtual string LastName { get; protected set; }
        public virtual DateTime DateOfBirth { get; protected set; }
        public virtual string MobilePhone { get; protected set; }

        public virtual string DisplayName
        {
            get { return LastName + ", " + FirstName; }
        }
    }
}
