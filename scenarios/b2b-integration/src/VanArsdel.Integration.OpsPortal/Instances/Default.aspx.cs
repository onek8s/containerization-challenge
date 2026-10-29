using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI.WebControls;
using AutoMapper;
using log4net;
using Microsoft.Practices.Unity;
using VanArsdel.Integration.Management.Operations;
using VanArsdel.Integration.OpsPortal.Models;

namespace VanArsdel.Integration.OpsPortal.Instances
{
    public partial class DefaultPage : OpsPage
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DefaultPage));

        [Dependency]
        public IInstanceOperations Operations { get; set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ServiceFilter.Text = Request.QueryString["service"];
                ErrorFilter.Text = Request.QueryString["error"];
                var app = Request.QueryString["app"];
                if (!string.IsNullOrEmpty(app) && ApplicationFilter.Items.FindByValue(app) != null)
                {
                    ApplicationFilter.SelectedValue = app;
                }
            }

            ActionButtons.Visible = CanOperate;
        }

        public IEnumerable<InstanceRow> GetInstances()
        {
            InstanceKind kind;
            var filter = new InstanceFilter
            {
                Application = ApplicationFilter.SelectedValue,
                ServiceContains = ServiceFilter.Text.Trim(),
                ErrorContains = ErrorFilter.Text.Trim(),
                Kind = Enum.TryParse(KindFilter.SelectedValue, out kind) ? kind : (InstanceKind?)null,
                MaxRows = 2000
            };

            return Mapper.Map<List<InstanceRow>>(Operations.Suspended(filter));
        }

        protected void Search_Click(object sender, EventArgs e)
        {
            InstanceGrid.PageIndex = 0;
            InstanceGrid.DataBind();
        }

        protected void Resume_Click(object sender, EventArgs e)
        {
            Apply("Resumed", Operations.Resume);
        }

        protected void Terminate_Click(object sender, EventArgs e)
        {
            Apply("Terminated", Operations.Terminate);
        }

        private void Apply(string verb, Func<Guid, InstanceActionResult> action)
        {
            Demand(PageAccess.Operate);
            var ids = SelectedIds().ToList();
            if (ids.Count == 0)
            {
                Flash("Select at least one instance.", true);
                Response.Redirect(Request.RawUrl, false);
                return;
            }

            var failed = new List<string>();
            foreach (var id in ids)
            {
                var result = action(id);
                if (!result.Succeeded)
                {
                    failed.Add(result.Message);
                }
            }

            Log.InfoFormat("{0} {1} instance(s) by {2}, {3} failed", verb, ids.Count - failed.Count, UserName, failed.Count);
            Flash(failed.Count == 0
                ? string.Format("{0} {1} instance(s).", verb, ids.Count)
                : string.Format("{0} {1} of {2}; failed: {3}", verb, ids.Count - failed.Count, ids.Count, string.Join("; ", failed)), failed.Count > 0);
            Response.Redirect(Request.RawUrl, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private IEnumerable<Guid> SelectedIds()
        {
            foreach (GridViewRow row in InstanceGrid.Rows)
            {
                var box = (CheckBox)row.FindControl("Selected");
                if (box != null && box.Checked)
                {
                    yield return (Guid)InstanceGrid.DataKeys[row.RowIndex].Value;
                }
            }
        }
    }
}
