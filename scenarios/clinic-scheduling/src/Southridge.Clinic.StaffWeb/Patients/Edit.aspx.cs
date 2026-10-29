using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb.Patients
{
    public partial class EditPage : ClinicPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        private int? PatientId
        {
            get { return QueryInt("id"); }
        }

        /// <summary>Row version of the loaded patient, for the optimistic concurrency check on save.</summary>
        private byte[] LoadedVersion
        {
            get { return (byte[])ViewState["RowVersion"]; }
            set { ViewState["RowVersion"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack)
            {
                return;
            }

            if (PatientId.HasValue)
            {
                EditViews.SetActiveView(UpdateView);
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    Patient p = db.Patients.SingleOrDefault(x => x.PatientId == PatientId.Value);
                    if (p == null)
                    {
                        ShowError("Patient not found.");
                        SaveButton.Visible = false;
                        return;
                    }

                    EditFields.LoadPatient(p);
                    EditName.Text = Server.HtmlEncode(p.LastName + ", " + p.FirstName);
                    EditMrn.Text = p.Mrn;
                    LoadedVersion = p.RowVersion.ToArray();
                    ChartLink.NavigateUrl = "~/Patients/Chart.aspx?id=" + p.PatientId;
                }
            }
            else
            {
                EditViews.SetActiveView(RegisterView);
                PayerList.DataSource = StaffCache.Payers();
                PayerList.DataBind();
                EffectiveBox.Text = new DateTime(DateTime.Today.Year, 1, 1).ToString("MM/dd/yyyy", Us);
            }
        }

        protected void RegisterWizard_NextButtonClick(object sender, WizardNavigationEventArgs e)
        {
            if (e.CurrentStepIndex == 0)
            {
                NewPatient candidate;
                try
                {
                    candidate = RegisterFields.ToNewPatient();
                    PatientService.Validate(candidate);
                }
                catch (ClinicRuleException ex)
                {
                    ShowError(ex.Message);
                    e.Cancel = true;
                    return;
                }

                List<PatientSearchRow> duplicates = new PatientRepository().FindDuplicates(candidate.LastName, candidate.DateOfBirth)
                    .Where(d => PatientIdentity.LikelySamePerson(d.LastName, d.FirstName, d.DateOfBirth, candidate.LastName, candidate.FirstName, candidate.DateOfBirth))
                    .ToList();
                if (duplicates.Count > 0 && !NotDuplicateBox.Checked)
                {
                    DuplicateGrid.DataSource = duplicates;
                    DuplicateGrid.DataBind();
                    DuplicatePanel.Visible = true;
                    e.Cancel = true;
                }
            }
            else if (e.CurrentStepIndex == 1)
            {
                ReviewName.Text = Server.HtmlEncode(RegisterFields.DisplayName);
                ReviewCoverage.Text = SelfPayBox.Checked ? "no insurance" : Server.HtmlEncode(PayerList.SelectedItem.Text + " " + MemberIdBox.Text);
            }
        }

        protected void RegisterWizard_FinishButtonClick(object sender, WizardNavigationEventArgs e)
        {
            try
            {
                RegistrationResult result = new PatientService().Register(RegisterFields.ToNewPatient(), UserName, NotDuplicateBox.Checked);
                if (result.PossibleDuplicate)
                {
                    ShowError("A matching patient already exists (" + result.Mrn + ").");
                    e.Cancel = true;
                    return;
                }

                if (!SelfPayBox.Checked && !string.IsNullOrEmpty(MemberIdBox.Text))
                {
                    DateTime effective;
                    decimal copay;
                    using (ClinicDataContext db = ClinicDataContextFactory.Create())
                    {
                        db.PatientInsurances.InsertOnSubmit(new PatientInsurance
                        {
                            PatientId = result.PatientId,
                            Sequence = 1,
                            PayerId = int.Parse(PayerList.SelectedValue),
                            MemberId = MemberIdBox.Text.Trim(),
                            GroupNumber = string.IsNullOrEmpty(GroupBox.Text) ? null : GroupBox.Text.Trim(),
                            Relationship = (byte)InsuredRelationship.Self,
                            Copay = decimal.TryParse(CopayBox.Text, NumberStyles.Currency, Us, out copay) ? copay : 0m,
                            EffectiveFrom = DateTime.TryParse(EffectiveBox.Text, Us, DateTimeStyles.None, out effective) ? effective : DateTime.Today
                        });
                        db.SubmitChanges();
                    }
                }

                Audit("RegisterPatient", "Patient", result.Mrn, result.PatientId, null);
                Response.Redirect("~/Patients/Chart.aspx?id=" + result.PatientId + "&new=1", false);
            }
            catch (Exception ex)
            {
                e.Cancel = true;
                HandleError(ex);
            }
        }

        protected void SaveButton_Click(object sender, EventArgs e)
        {
            if (!IsValid)
            {
                return;
            }

            try
            {
                var p = new Patient { PatientId = PatientId.Value, RowVersion = new System.Data.Linq.Binary(LoadedVersion) };
                EditFields.ApplyTo(p);
                new PatientService().Update(p, UserName);
                Audit("UpdatePatient", "Patient", PatientId.Value.ToString(), PatientId.Value, null);
                Response.Redirect("~/Patients/Chart.aspx?id=" + PatientId.Value, false);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }
    }
}
