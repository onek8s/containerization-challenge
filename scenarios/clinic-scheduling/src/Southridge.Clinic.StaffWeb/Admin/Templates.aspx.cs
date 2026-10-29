using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;

namespace Southridge.Clinic.StaffWeb.Admin
{
    public partial class TemplatesPage : ClinicPage
    {
        private const int MinimumSlotMinutes = 5;

        protected IList<ListItemData> Days
        {
            get
            {
                return Enumerable.Range(0, 7)
                    .Select(d => new ListItemData(d.ToString(CultureInfo.InvariantCulture), ((DayOfWeek)d).ToString()))
                    .ToList();
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ProviderList.DataSource = StaffCache.Providers(null);
                ProviderList.DataBind();
            }
        }

        protected void ProviderList_SelectedIndexChanged(object sender, EventArgs e)
        {
            TemplateGrid.EditIndex = -1;
        }

        protected void TemplateSource_Inserting(object sender, LinqDataSourceInsertEventArgs e)
        {
            var template = (ScheduleTemplate)e.NewObject;
            template.ProviderId = int.Parse(ProviderList.SelectedValue, CultureInfo.InvariantCulture);
            if (!Check(template))
            {
                e.Cancel = true;
            }
        }

        protected void TemplateSource_Updating(object sender, LinqDataSourceUpdateEventArgs e)
        {
            if (!Check((ScheduleTemplate)e.NewObject))
            {
                e.Cancel = true;
            }
        }

        protected void TemplateSource_Changed(object sender, LinqDataSourceStatusEventArgs e)
        {
            if (e.Exception != null)
            {
                HandleError(e.Exception);
                e.ExceptionHandled = true;
                return;
            }

            StaffCache.Invalidate();
            Audit("EditTemplate", "Provider", ProviderList.SelectedValue, null, null);
        }

        private bool Check(ScheduleTemplate t)
        {
            if (t.EndTime <= t.StartTime)
            {
                ShowError("The session must end after it starts.");
                return false;
            }

            if (t.SlotMinutes < MinimumSlotMinutes || (t.EndTime - t.StartTime).TotalMinutes % t.SlotMinutes != 0)
            {
                ShowError("The session length must be a multiple of the slot length (at least " + MinimumSlotMinutes + " minutes).");
                return false;
            }

            if (t.EffectiveTo.HasValue && t.EffectiveTo.Value < t.EffectiveFrom)
            {
                ShowError("The end date is before the start date.");
                return false;
            }

            if (!string.IsNullOrEmpty(t.AppointmentTypes))
            {
                t.AppointmentTypes = t.AppointmentTypes.Replace(" ", string.Empty).ToUpperInvariant();
            }

            return true;
        }
    }
}
