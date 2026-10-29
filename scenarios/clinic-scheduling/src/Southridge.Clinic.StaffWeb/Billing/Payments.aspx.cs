using System;
using System.Globalization;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;

namespace Southridge.Clinic.StaffWeb.Billing
{
    public partial class PaymentsPage : ClinicPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                int? patientId = QueryInt("patientId");
                if (patientId.HasValue)
                {
                    PatientId.Value = patientId.Value.ToString(CultureInfo.InvariantCulture);
                    ShowPatient();
                }
            }
        }

        protected void LoadButton_Click(object sender, EventArgs e)
        {
            ShowPatient();
        }

        protected void PostButton_Click(object sender, EventArgs e)
        {
            int patientId;
            if (!int.TryParse(PatientId.Value, out patientId))
            {
                ShowError("Choose the patient from the list.");
                return;
            }

            if (!IsValid)
            {
                return;
            }

            try
            {
                decimal amount = decimal.Parse(AmountBox.Text, NumberStyles.Currency, Us);
                new PatientRepository().PostPatientPayment(patientId, amount, MethodList.SelectedValue, ReferenceBox.Text.Trim(), UserName);
                AmountBox.Text = ReferenceBox.Text = string.Empty;
                ShowPatient();
                ShowMessage(amount.ToString("C", Us) + " posted.");
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void ShowPatient()
        {
            int patientId;
            if (!int.TryParse(PatientId.Value, out patientId))
            {
                BalanceLabel.Text = string.Empty;
                ListTitle.Text = "Payments posted today";
                PaymentGrid.DataBind();
                return;
            }

            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                Patient p = db.Patients.SingleOrDefault(x => x.PatientId == patientId);
                if (p == null)
                {
                    ShowError("The patient was not found.");
                    return;
                }

                PatientBox.Text = p.LastName + ", " + p.FirstName + "  " + p.DateOfBirth.ToString("MM/dd/yyyy", Us) + "  " + p.Mrn;
                BalanceLabel.Text = "Balance " + p.Balance.ToString("C", Us);
                ListTitle.Text = "Payments for " + Server.HtmlEncode(p.FirstName + " " + p.LastName);
            }

            PaymentGrid.PageIndex = 0;
            PaymentGrid.DataBind();
        }
    }
}
