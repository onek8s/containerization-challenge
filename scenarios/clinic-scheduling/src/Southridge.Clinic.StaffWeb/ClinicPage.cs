using System;
using System.Globalization;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.StaffWeb
{
    /// <summary>Base class for all staff pages: current site, messages and the UI exception policy.</summary>
    public class ClinicPage : Page
    {
        private const string SiteSessionKey = "CurrentSiteId";

        /// <summary>The clinic site the user is working at. Starts from the profile default, can be switched in the header.</summary>
        public int CurrentSiteId
        {
            get
            {
                object value = Session[SiteSessionKey];
                if (value == null)
                {
                    int site = User.Identity.IsAuthenticated ? StaffProfile.Current.DefaultSiteId : 0;
                    value = site > 0 ? site : 1;
                    Session[SiteSessionKey] = value;
                }

                return (int)value;
            }

            set
            {
                Session[SiteSessionKey] = value;
            }
        }

        public string UserName
        {
            get { return User.Identity.Name; }
        }

        protected SiteMaster SiteMaster
        {
            get
            {
                MasterPage master = Master;
                while (master != null && !(master is SiteMaster))
                {
                    master = master.Master;
                }

                return (SiteMaster)master;
            }
        }

        protected void ShowMessage(string message)
        {
            SiteMaster.ShowMessage(message, false);
        }

        protected void ShowError(string message)
        {
            SiteMaster.ShowMessage(message, true);
        }

        /// <summary>Shows rule violations to the user; logs anything else through the UI exception policy.</summary>
        protected void HandleError(Exception ex)
        {
            if (ex is ClinicRuleException)
            {
                ShowError(ex.Message);
                return;
            }

            Exception toThrow = ErrorHandler.Handle(ex, ExceptionPolicies.UserInterface);
            ShowError("Something went wrong. The error has been logged" + (toThrow != null ? " (" + toThrow.GetType().Name + ")." : "."));
        }

        protected int? QueryInt(string name)
        {
            int value;
            return int.TryParse(Request.QueryString[name], NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : (int?)null;
        }

        protected DateTime? QueryDate(string name)
        {
            DateTime value;
            return DateTime.TryParseExact(Request.QueryString[name], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value) ? value : (DateTime?)null;
        }

        protected bool IsInAnyRole(params string[] roles)
        {
            foreach (string role in roles)
            {
                if (User.IsInRole(role))
                {
                    return true;
                }
            }

            return false;
        }

        protected void Audit(string action, string entityType, string entityId, int? patientId, string details)
        {
            new Southridge.Clinic.Data.Repositories.SystemRepository().Audit(UserName, action, entityType, entityId, patientId, details, Request.UserHostAddress);
        }

        protected static string Money(object value)
        {
            return value == null || value == DBNull.Value ? string.Empty : Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("C", CultureInfo.GetCultureInfo("en-US"));
        }
    }
}
