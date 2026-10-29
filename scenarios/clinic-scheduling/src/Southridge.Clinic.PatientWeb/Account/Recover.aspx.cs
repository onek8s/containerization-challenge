using System.Web.UI.WebControls;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.PatientWeb.Account
{
    public partial class RecoverPage : PortalPage
    {
        protected void Recovery_SendingMail(object sender, MailMessageEventArgs e)
        {
            ClinicLog.Audit("portal:" + Recovery.UserName, "PasswordReset", Request.UserHostAddress);
        }
    }
}
