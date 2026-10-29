using System;
using System.Web.Security;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.PatientWeb.Account
{
    public partial class RegisterPage : PortalPage
    {
        protected void CreateAccount_CreatedUser(object sender, EventArgs e)
        {
            if (!Roles.IsUserInRole(CreateAccount.UserName, ClinicRoles.Patient))
            {
                Roles.AddUserToRole(CreateAccount.UserName, ClinicRoles.Patient);
            }

            ClinicLog.Audit("portal:" + CreateAccount.UserName, "PortalAccountCreated", Request.UserHostAddress);
        }
    }
}
