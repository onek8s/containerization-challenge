using System;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;

namespace Southridge.Clinic.StaffWeb.Patients
{
    public partial class SearchPage : ClinicPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                PatientGrid.PageSize = StaffProfile.Current.GridPageSize;
                LastNameBox.Text = Request.QueryString["q"];
            }
        }

        protected void SearchButton_Click(object sender, EventArgs e)
        {
            PatientGrid.PageIndex = 0;
            PatientGrid.DataBind();
        }

        protected void PatientGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow && ((PatientSearchRow)e.Row.DataItem).Balance > 0)
            {
                e.Row.CssClass = "balance";
            }
        }
    }
}
