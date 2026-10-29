using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Referrals;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb.Referrals
{
    public partial class ReferralPage : ReferralPageBase
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        private Referral _referral;

        private Referral Current
        {
            get
            {
                if (_referral == null)
                {
                    _referral = Repository.Get(QueryInt("id") ?? 0);
                }

                return _referral;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Current == null)
            {
                ShowError("The referral was not found.");
                ActionsPanel.Visible = NotePanel.Visible = UploadButton.Visible = DocumentUpload.Visible = false;
                return;
            }

            if (!IsPostBack)
            {
                Bind();
                Audit("ViewReferral", "Referral", Current.ReferralNumber, Current.Patient.Id, null);
                if (Request.QueryString["new"] == "1")
                {
                    ShowMessage("Referral " + Current.ReferralNumber + " saved.");
                }
            }
        }

        protected void ReviewButton_Click(object sender, EventArgs e)
        {
            if (Commit(() => Service.StartReview(Current, UserName)))
            {
                Bind();
            }
        }

        protected void ApproveButton_Click(object sender, EventArgs e)
        {
            if (!CanDecide())
            {
                return;
            }

            int? visits = VisitsBox.Text.Trim().Length > 0 ? int.Parse(VisitsBox.Text, CultureInfo.InvariantCulture) : (int?)null;
            string note = DecisionNoteBox.Text.Trim();
            if (Commit(() => Service.Approve(Current, UserName, AuthorizationBox.Text, visits, note)))
            {
                ReferralWorkflowClient.Decided(Current.Id, true, UserName, note);
                Bind();
                ShowMessage("Referral approved. The approval letter is sent by the referral workflow.");
            }
        }

        protected void DeclineButton_Click(object sender, EventArgs e)
        {
            if (!CanDecide())
            {
                return;
            }

            string note = DecisionNoteBox.Text.Trim();
            if (Commit(() => Service.Decline(Current, UserName, note)))
            {
                ReferralWorkflowClient.Decided(Current.Id, false, UserName, note);
                Bind();
                ShowMessage("Referral declined.");
            }
        }

        protected void CancelButton_Click(object sender, EventArgs e)
        {
            string reason = CancelReasonBox.Text.Trim();
            if (IsValid && Commit(() => Service.Cancel(Current, UserName, reason)))
            {
                ReferralWorkflowClient.Cancelled(Current.Id, UserName, reason);
                Bind();
            }
        }

        protected void UploadButton_Click(object sender, EventArgs e)
        {
            if (!DocumentUpload.HasFile)
            {
                ShowError("Choose a file to attach.");
                return;
            }

            string name = Path.GetFileName(DocumentUpload.FileName);
            if (Commit(() => Current.AddDocument(name, DocumentUpload.PostedFile.ContentType, DocumentUpload.FileBytes, UserName, DateTime.Now)))
            {
                Bind();
            }
        }

        protected void AddNoteButton_Click(object sender, EventArgs e)
        {
            string text = NoteBox.Text.Trim();
            if (IsValid && Commit(() => Current.AddNote(UserName, text, DateTime.Now)))
            {
                NoteBox.Text = string.Empty;
                Bind();
            }
        }

        /// <summary>Approval needs a provider or the practice manager; coordinators prepare the referral.</summary>
        private bool CanDecide()
        {
            if (!IsInAnyRole(ClinicRoles.Provider, ClinicRoles.Manager, ClinicRoles.Administrator))
            {
                ShowError("Only a provider or the practice manager can approve or decline a referral.");
                return false;
            }

            return IsValid;
        }

        private void Bind()
        {
            Referral r = Current;
            NumberLabel.Text = r.ReferralNumber;
            StatusLabel.Text = r.Status.ToString();
            string requested = null;
            if (r.RequestedProviderId.HasValue)
            {
                Provider provider = ClinicLookups.Providers(null, null).FirstOrDefault(p => p.ProviderId == r.RequestedProviderId.Value);
                requested = provider == null ? "#" + r.RequestedProviderId : ClinicLookups.ProviderName(provider);
            }

            string appointmentText = null;
            if (r.AppointmentId.HasValue)
            {
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    Appointment a = db.Appointments.SingleOrDefault(x => x.AppointmentId == r.AppointmentId.Value);
                    appointmentText = a == null ? "#" + r.AppointmentId : a.StartsAt.ToString("MM/dd/yyyy h:mm tt", Us) + " (" + a.ConfirmationCode + ")";
                }
            }

            ReferralDetails.DataSource = new[]
            {
                new
                {
                    PatientId = r.Patient.Id,
                    PatientName = r.Patient.DisplayName + " (" + r.Patient.Mrn + ")",
                    r.Priority,
                    r.SpecialtyCode,
                    RequestedProvider = requested,
                    ReferringProvider = r.ReferringProviderName + (r.ReferringProviderNpi == null ? string.Empty : " NPI " + r.ReferringProviderNpi)
                        + (r.ReferringPractice == null ? string.Empty : ", " + r.ReferringPractice)
                        + (r.ReferringPhone == null ? string.Empty : ", " + PhoneNumber.Format(r.ReferringPhone)),
                    r.Reason,
                    r.DiagnosisCode,
                    Received = r.ReceivedOn.ToString("MM/dd/yyyy h:mm tt", Us) + " by " + r.Source,
                    r.DueBy,
                    Decision = r.DecidedOn.HasValue ? r.Status + " by " + r.DecidedBy + " " + r.DecidedOn.Value.ToString("MM/dd/yyyy", Us) + (string.IsNullOrEmpty(r.DecisionNote) ? string.Empty : ": " + r.DecisionNote) : null,
                    Authorization = r.AuthorizationNumber == null ? null : r.AuthorizationNumber + (r.VisitsAuthorized.HasValue ? ", " + r.VisitsAuthorized + " visits" : string.Empty)
                        + (r.ExpiresOn.HasValue ? ", expires " + r.ExpiresOn.Value.ToString("MM/dd/yyyy", Us) : string.Empty),
                    AppointmentId = r.AppointmentId,
                    AppointmentText = appointmentText
                }
            };
            ReferralDetails.DataBind();

            ReviewButton.Visible = r.Status == ReferralStatus.Received;
            DecisionFields.Visible = r.Status == ReferralStatus.Received || r.Status == ReferralStatus.InReview;
            CancelFields.Visible = r.IsOpen;
            BookLink.Visible = r.Status == ReferralStatus.Approved;
            BookLink.NavigateUrl = "~/Schedule/Book.aspx?patientId=" + r.Patient.Id + "&referralId=" + r.Id;

            DocumentRepeater.DataSource = r.Documents.OrderBy(d => d.UploadedOn).Select(d => new { d.Id, d.FileName, d.UploadedOn, d.UploadedBy }).ToList();
            DocumentRepeater.DataBind();
            NoteGrid.DataSource = r.Notes.OrderByDescending(n => n.CreatedOn).ToList();
            NoteGrid.DataBind();
        }
    }
}
