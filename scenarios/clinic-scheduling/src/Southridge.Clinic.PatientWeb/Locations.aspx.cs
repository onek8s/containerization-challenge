using System;

namespace Southridge.Clinic.PatientWeb
{
    public partial class LocationsPage : PortalPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            TryBooking(delegate
            {
                SiteRepeater.DataSource = Booking.GetSites();
                SiteRepeater.DataBind();
            });
        }
    }
}
