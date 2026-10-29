using System;
using System.Web.UI.WebControls;

namespace Southridge.Clinic.StaffWeb.Admin
{
    public partial class SettingsPage : ClinicPage
    {
        protected void SettingSource_Updated(object sender, ObjectDataSourceStatusEventArgs e)
        {
            if (e.Exception != null)
            {
                HandleError(e.Exception.InnerException ?? e.Exception);
                e.ExceptionHandled = true;
                return;
            }

            StaffCache.Invalidate();
            ShowMessage("Setting saved.");
        }
    }
}
