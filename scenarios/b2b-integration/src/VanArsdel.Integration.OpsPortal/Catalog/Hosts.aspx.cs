using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI.WebControls;
using AutoMapper;
using log4net;
using Microsoft.Practices.Unity;
using VanArsdel.Integration.Management.Wmi;
using VanArsdel.Integration.OpsPortal.Models;

namespace VanArsdel.Integration.OpsPortal.Catalog
{
    public partial class HostsPage : OpsPage
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HostsPage));

        [Dependency]
        public IHostInstances HostInstances { get; set; }

        public IEnumerable<HostRow> GetHosts()
        {
            return Mapper.Map<List<HostRow>>(HostInstances.All()).OrderBy(h => h.HostName).ThenBy(h => h.ServerName);
        }

        protected void HostGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "StartHost" && e.CommandName != "StopHost")
            {
                return;
            }

            Demand(PageAccess.Operate);
            var parts = ((string)e.CommandArgument).Split('|');
            try
            {
                if (e.CommandName == "StartHost")
                {
                    HostInstances.Start(parts[0], parts[1]);
                }
                else
                {
                    HostInstances.Stop(parts[0], parts[1]);
                }

                Log.InfoFormat("{0} {1} on {2} by {3}", e.CommandName, parts[0], parts[1], UserName);
                Flash(string.Format("{0} sent to {1} on {2}.", e.CommandName == "StartHost" ? "Start" : "Stop", parts[0], parts[1]));
            }
            catch (Exception ex)
            {
                Log.Error("Host instance command failed", ex);
                Flash("Host instance command failed: " + ex.Message, true);
            }

            Response.Redirect(Request.RawUrl, false);
        }
    }
}
