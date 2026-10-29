using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb.Patients
{
    public partial class ChartPage : ClinicPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        private int PatientId
        {
            get { return QueryInt("id") ?? 0; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (!BindHeader())
                {
                    return;
                }

                BindView(0);
                Audit("ViewChart", "Patient", PatientId.ToString(), PatientId, null);
                if (Request.QueryString["new"] == "1")
                {
                    ShowMessage("Patient registered.");
                }
            }
        }

        protected void ChartTabs_MenuItemClick(object sender, MenuEventArgs e)
        {
            int index = int.Parse(e.Item.Value);
            ChartViews.ActiveViewIndex = index;
            BindView(index);
        }

        protected void LabGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                string flag = ((LabResult)e.Row.DataItem).AbnormalFlag;
                if (!string.IsNullOrEmpty(flag) && flag != "N")
                {
                    e.Row.CssClass = "abnormal";
                }
            }
        }

        protected void AddNoteButton_Click(object sender, EventArgs e)
        {
            if (!IsValid)
            {
                return;
            }

            using (ClinicDataContext db = ClinicDataContextFactory.Create())
            {
                db.PatientNotes.InsertOnSubmit(new PatientNote
                {
                    PatientId = PatientId,
                    NoteType = NoteTypeList.SelectedValue,
                    Text = NoteBox.Text.Trim().Length > 2000 ? NoteBox.Text.Trim().Substring(0, 2000) : NoteBox.Text.Trim(),
                    IsAlert = AlertBox.Checked,
                    CreatedBy = UserName,
                    CreatedOn = DateTime.Now
                });
                db.SubmitChanges();
            }

            NoteBox.Text = string.Empty;
            AlertBox.Checked = false;
            BindHeader();
            BindView(4);
        }

        private bool BindHeader()
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                Patient p = db.Patients.SingleOrDefault(x => x.PatientId == PatientId);
                if (p == null)
                {
                    ShowError("Patient not found.");
                    ChartTabs.Visible = ChartViews.Visible = false;
                    return false;
                }

                Title = p.LastName + ", " + p.FirstName;
                PatientName.Text = Server.HtmlEncode(p.LastName + ", " + p.FirstName + (p.MiddleName == null ? string.Empty : " " + p.MiddleName));
                Mrn.Text = p.Mrn;
                Dob.Text = p.DateOfBirth.ToString("MM/dd/yyyy", Us);
                int years = PatientIdentity.AgeOn(p.DateOfBirth, DateTime.Today);
                Age.Text = years >= 2 ? years + " y" : ((int)(DateTime.Today - p.DateOfBirth).TotalDays / 30) + " mo";
                Phone.Text = PhoneNumber.Format(p.MobilePhone ?? p.HomePhone);
                Balance.Text = p.Balance.ToString("C", Us);
                AlertRepeater.DataSource = p.PatientNotes.Where(n => n.IsAlert).OrderByDescending(n => n.CreatedOn).ToList();
                AlertRepeater.DataBind();
                EditLink.NavigateUrl = "~/Patients/Edit.aspx?id=" + p.PatientId;
                BookLink.NavigateUrl = "~/Schedule/Book.aspx?patientId=" + p.PatientId;
                ReferralLink.NavigateUrl = "~/Referrals/New.aspx?patientId=" + p.PatientId;
                StatementLink.NavigateUrl = "~/Handlers/Statement.ashx?id=" + p.PatientId;
                PaymentLink.NavigateUrl = "~/Billing/Payments.aspx?patientId=" + p.PatientId;
                StatementLink.Visible = PaymentLink.Visible = IsInAnyRole("Billing", "FrontDesk", "PracticeManager", "Administrator");
                return true;
            }
        }

        private void BindView(int index)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                switch (index)
                {
                    case 0:
                        AppointmentGrid.DataSource = ClinicLookups.PatientAppointments(PatientId, DateTime.Today.AddYears(-2), DateTime.Today.AddYears(1))
                            .OrderByDescending(a => a.StartsAt).ToList();
                        AppointmentGrid.DataBind();
                        break;
                    case 1:
                        VisitGrid.DataSource = db.Encounters.Where(x => x.PatientId == PatientId).OrderByDescending(x => x.DateOfService).ToList()
                            .Select(x => new
                            {
                                x.EncounterId,
                                x.DateOfService,
                                ProviderName = ClinicLookups.ProviderName(x.Provider),
                                SiteName = x.Site.Name,
                                x.ChiefComplaint,
                                Diagnoses = string.Join(", ", x.EncounterDiagnosis.OrderBy(d => d.Sequence).Select(d => d.Code).ToArray()),
                                StatusText = ((EncounterStatus)x.Status).ToString()
                            }).ToList();
                        VisitGrid.DataBind();
                        break;
                    case 2:
                        LabGrid.DataSource = db.LabResults.Where(r => r.PatientId == PatientId).OrderByDescending(r => r.ObservedOn).ThenBy(r => r.ObservationName).ToList();
                        LabGrid.DataBind();
                        break;
                    case 3:
                        InsuranceGrid.DataSource = db.PatientInsurances.Where(i => i.PatientId == PatientId).OrderBy(i => i.Sequence).ToList()
                            .Select(i => new { i.Sequence, PayerName = i.Payer.Name, i.MemberId, i.GroupNumber, i.Copay, i.EffectiveFrom, i.EffectiveTo, i.VerifiedOn })
                            .ToList();
                        InsuranceGrid.DataBind();
                        break;
                    case 4:
                        NoteRepeater.DataSource = db.PatientNotes.Where(n => n.PatientId == PatientId).OrderByDescending(n => n.CreatedOn).ToList();
                        NoteRepeater.DataBind();
                        break;
                    case 5:
                        var billing = new BillingRepository();
                        ClaimGrid.DataSource = billing.ClaimsForPatient(PatientId, DateTime.Today.AddYears(-3), DateTime.Today);
                        ClaimGrid.DataBind();
                        StatementGrid.DataSource = billing.StatementsForPatient(PatientId, 12);
                        StatementGrid.DataBind();
                        break;
                }
            }
        }
    }
}
