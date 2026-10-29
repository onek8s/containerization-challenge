using System;

namespace Southridge.Clinic.StaffWeb
{
    public partial class ErrorPage : ClinicPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
        }
    }
}
