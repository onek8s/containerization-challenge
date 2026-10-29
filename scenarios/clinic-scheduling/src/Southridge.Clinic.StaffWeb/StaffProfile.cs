using System;
using System.Web;
using System.Web.Profile;

namespace Southridge.Clinic.StaffWeb
{
    /// <summary>
    /// Typed ASP.NET profile for staff users (SqlProfileProvider). Registered with profile inherits="..." in Web.config.
    /// </summary>
    public class StaffProfile : ProfileBase
    {
        public static StaffProfile Current
        {
            get { return (StaffProfile)HttpContext.Current.Profile; }
        }

        public static StaffProfile For(string userName)
        {
            return (StaffProfile)Create(userName, true);
        }

        [SettingsAllowAnonymous(false)]
        public int DefaultSiteId
        {
            get { return (int)GetPropertyValue("DefaultSiteId"); }
            set { SetPropertyValue("DefaultSiteId", value); }
        }

        [SettingsAllowAnonymous(false)]
        public int? ProviderId
        {
            get { return (int?)GetPropertyValue("ProviderId"); }
            set { SetPropertyValue("ProviderId", value); }
        }

        [SettingsAllowAnonymous(false)]
        public string FullName
        {
            get { return (string)GetPropertyValue("FullName"); }
            set { SetPropertyValue("FullName", value); }
        }

        [SettingsAllowAnonymous(false)]
        public int GridPageSize
        {
            get
            {
                int size = (int)GetPropertyValue("GridPageSize");
                return size <= 0 ? 25 : size;
            }

            set { SetPropertyValue("GridPageSize", value); }
        }

        [SettingsAllowAnonymous(false)]
        public bool ShowCancelled
        {
            get { return (bool)GetPropertyValue("ShowCancelled"); }
            set { SetPropertyValue("ShowCancelled", value); }
        }

        /// <summary>Referral coordinators usually work one specialty's queue.</summary>
        [SettingsAllowAnonymous(false)]
        public string ReferralSpecialty
        {
            get { return (string)GetPropertyValue("ReferralSpecialty"); }
            set { SetPropertyValue("ReferralSpecialty", value); }
        }

        [SettingsAllowAnonymous(false)]
        public DateTime LastLogin
        {
            get { return (DateTime)GetPropertyValue("LastLogin"); }
            set { SetPropertyValue("LastLogin", value); }
        }
    }
}
