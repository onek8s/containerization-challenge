using System;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;

namespace Southridge.Clinic.StaffWeb.Schedule
{
    public partial class WaitlistPage : ClinicPage
    {
        protected void WaitlistSource_Selecting(object sender, LinqDataSourceSelectEventArgs e)
        {
            e.WhereParameters["SiteId"] = CurrentSiteId;
        }

        protected void WaitlistGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "Resolve")
            {
                return;
            }

            int id = int.Parse((string)e.CommandArgument);
            using (ClinicDataContext db = ClinicDataContextFactory.Create())
            {
                WaitlistEntry entry = db.WaitlistEntries.Single(w => w.WaitlistEntryId == id);
                entry.ResolvedOn = DateTime.Now;
                db.SubmitChanges();
            }

            WaitlistGrid.DataBind();
        }
    }
}
