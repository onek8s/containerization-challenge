using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Referrals;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb.Referrals
{
    public partial class NewPage : ReferralPageBase
    {
        private static readonly string[] AllowedTypes = { "application/pdf", "image/tiff" };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                SpecialtyList.DataSource = StaffCache.Specialties();
                SpecialtyList.DataBind();
                BindProviders();

                int? patientId = QueryInt("patientId");
                if (patientId.HasValue)
                {
                    using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                    {
                        Patient p = db.Patients.SingleOrDefault(x => x.PatientId == patientId.Value);
                        if (p != null)
                        {
                            PatientId.Value = p.PatientId.ToString(CultureInfo.InvariantCulture);
                            PatientBox.Text = p.LastName + ", " + p.FirstName + "  " + p.DateOfBirth.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) + "  " + p.Mrn;
                        }
                    }
                }
            }
        }

        protected void SpecialtyList_SelectedIndexChanged(object sender, EventArgs e)
        {
            BindProviders();
        }

        protected void PatientRequired_ServerValidate(object source, ServerValidateEventArgs args)
        {
            int id;
            args.IsValid = int.TryParse(PatientId.Value, out id) && id > 0;
        }

        protected void NpiValid_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = args.Value.Trim().Length == 0 || Npi.IsValid(args.Value.Trim());
        }

        protected void SaveButton_Click(object sender, EventArgs e)
        {
            if (!IsValid)
            {
                return;
            }

            if (LetterUpload.HasFile && !AllowedTypes.Contains(LetterUpload.PostedFile.ContentType))
            {
                ShowError("The referral letter must be a PDF or TIFF file.");
                return;
            }

            Referral referral = null;
            bool saved = Commit(delegate
            {
                referral = Service.Receive(new NewReferral
                {
                    PatientId = int.Parse(PatientId.Value, CultureInfo.InvariantCulture),
                    ReferringProviderName = ReferringNameBox.Text,
                    ReferringProviderNpi = NpiBox.Text.Trim().Length > 0 ? NpiBox.Text.Trim() : null,
                    ReferringPractice = PracticeBox.Text.Trim().Length > 0 ? PracticeBox.Text.Trim() : null,
                    ReferringPhone = PhoneBox.Text,
                    SpecialtyCode = SpecialtyList.SelectedValue,
                    RequestedProviderId = ProviderList.SelectedValue.Length > 0 ? int.Parse(ProviderList.SelectedValue, CultureInfo.InvariantCulture) : (int?)null,
                    Priority = (ReferralPriority)int.Parse(PriorityList.SelectedValue, CultureInfo.InvariantCulture),
                    Reason = ReasonBox.Text,
                    DiagnosisCode = DiagnosisBox.Text,
                    Source = SourceList.SelectedValue
                }, UserName);

                if (LetterUpload.HasFile)
                {
                    referral.AddDocument(Path.GetFileName(LetterUpload.FileName), LetterUpload.PostedFile.ContentType, LetterUpload.FileBytes, UserName, DateTime.Now);
                }
            });

            if (!saved)
            {
                return;
            }

            Audit("ReferralReceived", "Referral", referral.ReferralNumber, referral.Patient.Id, null);
            ReferralWorkflowClient.Start(referral.Id);
            Response.Redirect("~/Referrals/Referral.aspx?id=" + referral.Id + "&new=1", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void BindProviders()
        {
            ProviderList.Items.Clear();
            ProviderList.Items.Add(new ListItem("(first available)", string.Empty));
            ProviderList.DataSource = ClinicLookups.Providers(null, SpecialtyList.SelectedValue)
                .Select(p => new { p.ProviderId, Name = ClinicLookups.ProviderName(p) });
            ProviderList.DataBind();
        }
    }
}
