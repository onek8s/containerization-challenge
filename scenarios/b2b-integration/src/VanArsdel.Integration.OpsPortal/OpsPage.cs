using System;
using System.Web;
using System.Web.UI;

namespace VanArsdel.Integration.OpsPortal
{
    public enum PageAccess
    {
        View,
        Operate,
        Administer
    }

    /// <summary>Base page: Unity property injection, access check by Windows group and the flash message.</summary>
    public abstract class OpsPage : Page
    {
        private const string FlashKey = "ops.flash";

        protected virtual PageAccess Access
        {
            get { return PageAccess.View; }
        }

        protected string UserName
        {
            get { return User.Identity.Name; }
        }

        protected bool CanOperate
        {
            get { return PortalRoles.CanOperate(User); }
        }

        protected bool IsAdmin
        {
            get { return PortalRoles.IsAdmin(User); }
        }

        protected override void OnPreInit(EventArgs e)
        {
            ContainerConfig.BuildUp(this);
            base.OnPreInit(e);
        }

        protected override void OnInit(EventArgs e)
        {
            if (!Allowed(Access))
            {
                throw new HttpException(403, "You are not a member of the group this page needs.");
            }

            base.OnInit(e);
        }

        protected void Demand(PageAccess access)
        {
            if (!Allowed(access))
            {
                throw new HttpException(403, "You are not allowed to do that.");
            }
        }

        protected void Flash(string message, bool error = false)
        {
            Session[FlashKey] = (error ? "!" : string.Empty) + message;
        }

        /// <summary>Flash message for the master page; read once.</summary>
        public string TakeFlash()
        {
            var value = Session[FlashKey] as string;
            Session.Remove(FlashKey);
            return value;
        }

        protected Guid QueryGuid(string name)
        {
            Guid value;
            if (!Guid.TryParse(Request.QueryString[name], out value))
            {
                throw new HttpException(400, "Missing or invalid '" + name + "'.");
            }

            return value;
        }

        private bool Allowed(PageAccess access)
        {
            switch (access)
            {
                case PageAccess.Administer:
                    return PortalRoles.IsAdmin(User);
                case PageAccess.Operate:
                    return PortalRoles.CanOperate(User);
                default:
                    return PortalRoles.CanView(User);
            }
        }
    }
}
