using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Referrals;

namespace Southridge.Clinic.StaffWeb.Referrals
{
    public partial class QueuePage : ClinicPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                SpecialtyList.DataSource = StaffCache.Specialties();
                SpecialtyList.DataBind();
                string specialty = StaffProfile.Current.ReferralSpecialty;
                if (!string.IsNullOrEmpty(specialty) && SpecialtyList.Items.FindByValue(specialty) != null)
                {
                    SpecialtyList.SelectedValue = specialty;
                }

                IDictionary<ReferralStatus, int> counts = new ReferralRepository(ReferralSessions.Current).CountByStatus();
                CountsLabel.Text = string.Join(" &middot; ", counts.OrderBy(c => c.Key).Select(c => c.Key + ": " + c.Value).ToArray());
            }
        }

        protected void SearchButton_Click(object sender, EventArgs e)
        {
            bool queue = StatusList.SelectedValue == "open" && SearchBox.Text.Trim().Length == 0;
            if (StatusList.SelectedValue == "open" && !queue)
            {
                StatusList.SelectedValue = string.Empty;
            }

            ReferralGrid.DataSourceID = queue ? QueueSource.ID : SearchSource.ID;
            ReferralGrid.PageIndex = 0;
            ReferralGrid.DataBind();
        }

        protected void ReferralGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow)
            {
                return;
            }

            var referral = (Referral)e.Row.DataItem;
            if (referral.IsOverdue(DateTime.Now))
            {
                e.Row.CssClass = "overdue";
            }
            else if (referral.Priority != ReferralPriority.Routine && referral.IsOpen)
            {
                e.Row.CssClass = "urgent";
            }
        }
    }
}
