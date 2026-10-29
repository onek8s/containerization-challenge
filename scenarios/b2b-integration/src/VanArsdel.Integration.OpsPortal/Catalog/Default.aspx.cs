using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.UI.WebControls;
using log4net;
using Microsoft.Practices.Unity;
using VanArsdel.Integration.Management.Catalog;

namespace VanArsdel.Integration.OpsPortal.Catalog
{
    public partial class DefaultPage : OpsPage
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DefaultPage));

        [Dependency]
        public IGroupCatalog GroupCatalog { get; set; }

        public IEnumerable<ApplicationSummary> GetApplications()
        {
            return GroupCatalog.Applications(ConfigurationManager.AppSettings["Portal.ApplicationPrefix"] ?? "VanArsdel.");
        }

        protected void Location_Command(object source, RepeaterCommandEventArgs e)
        {
            Demand(PageAccess.Operate);
            var parts = ((string)e.CommandArgument).Split('|');
            var enable = e.CommandName == "Enable";
            Run(() => GroupCatalog.SetReceiveLocationEnabled(parts[0], parts[1], enable), (enable ? "Enabled " : "Disabled ") + parts[1]);
        }

        protected void SendPort_Command(object source, RepeaterCommandEventArgs e)
        {
            Demand(PageAccess.Operate);
            var name = (string)e.CommandArgument;
            var start = e.CommandName == "Start";
            Run(() => GroupCatalog.SetSendPortStarted(name, start), (start ? "Started " : "Stopped ") + name);
        }

        private void Run(Action change, string done)
        {
            try
            {
                change();
                Log.InfoFormat("{0} by {1}", done, UserName);
                Flash(done + ".");
            }
            catch (Exception ex)
            {
                Log.Error(done + " failed", ex);
                Flash(done + " failed: " + ex.Message, true);
            }

            Response.Redirect(Request.RawUrl, false);
        }
    }
}
