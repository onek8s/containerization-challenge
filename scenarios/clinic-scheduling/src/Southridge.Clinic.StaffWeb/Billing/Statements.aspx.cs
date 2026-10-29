using System;
using System.Globalization;
using System.Web.UI.WebControls;
using Southridge.Clinic.Billing;
using Southridge.Clinic.Data.Repositories;

namespace Southridge.Clinic.StaffWeb.Billing
{
    public partial class StatementsPage : ClinicPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindCandidates();
            }
        }

        protected void ShowButton_Click(object sender, EventArgs e)
        {
            if (IsValid)
            {
                CandidateGrid.PageIndex = 0;
                BindCandidates();
            }
        }

        protected void CandidateGrid_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            CandidateGrid.PageIndex = e.NewPageIndex;
            BindCandidates();
        }

        /// <summary>Records the statement (so the nightly run skips the patient) and sends the PDF to the browser for printing.</summary>
        protected void CandidateGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "Issue")
            {
                return;
            }

            int patientId = int.Parse((string)e.CommandArgument, CultureInfo.InvariantCulture);
            try
            {
                string fileName = "statement-" + patientId + "-" + DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".pdf";
                StatementResult result = new StatementService().Issue(patientId, DateTime.Today, "printed:" + fileName);
                if (result == null)
                {
                    ShowError("The patient has nothing to bill.");
                    return;
                }

                Audit("IssueStatement", "Statement", result.StatementId.ToString(CultureInfo.InvariantCulture), patientId, result.AmountDue.ToString("C", Us));
                Response.Clear();
                Response.ContentType = "application/pdf";
                Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName);
                Response.BinaryWrite(result.Pdf);
                Response.Flush();
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void BindCandidates()
        {
            decimal minimum;
            if (!decimal.TryParse(MinimumBox.Text, NumberStyles.Currency, Us, out minimum))
            {
                minimum = 10m;
            }

            CandidateGrid.DataSource = new BillingRepository().StatementCandidates(DateTime.Today, minimum);
            CandidateGrid.DataBind();
        }
    }
}
