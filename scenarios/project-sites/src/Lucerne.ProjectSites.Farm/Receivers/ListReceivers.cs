using System;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Domain;
using Microsoft.SharePoint;

namespace Lucerne.ProjectSites.Farm.Receivers
{
    /// <summary>Hub "Site Requests": validates the project number and queues the request for the provisioning job.</summary>
    public class SiteRequestReceiver : SPItemEventReceiver
    {
        public override void ItemAdding(SPItemEventProperties properties)
        {
            var number = Convert.ToString(properties.AfterProperties[FieldNames.ProjectNumber]);
            if (!Project.IsValidNumber(number))
            {
                properties.Status = SPEventReceiverStatus.CancelWithError;
                properties.ErrorMessage = "Enter the ERP project number (P followed by five digits).";
            }
        }

        public override void ItemAdded(SPItemEventProperties properties)
        {
            var item = properties.ListItem;
            EventFiringEnabled = false;
            try
            {
                item[FieldNames.RequestStatus] = SiteRequestStatus.Queued.ToString();
                if (item[FieldNames.Requester] == null)
                {
                    item[FieldNames.Requester] = new SPFieldUserValue(properties.Web, properties.CurrentUserId, null);
                }

                item.SystemUpdate(false);
            }
            finally
            {
                EventFiringEnabled = true;
            }
        }
    }

    /// <summary>Project site "Deliverables": revision rules and issued date.</summary>
    public class DeliverableReceiver : SPItemEventReceiver
    {
        public override void ItemUpdating(SPItemEventProperties properties)
        {
            var before = properties.ListItem[FieldNames.Revision] as string;
            var after = properties.AfterProperties[FieldNames.Revision] as string;
            if (after != null)
            {
                var error = RevisionRules.ValidateChange(before, after);
                if (error != null)
                {
                    properties.Status = SPEventReceiverStatus.CancelWithError;
                    properties.ErrorMessage = error;
                    return;
                }
            }

            var status = properties.AfterProperties[FieldNames.DeliverableStatus] as string;
            if (status == DeliverableStatus.Issued.ToString() && properties.ListItem[FieldNames.IssuedDate] == null)
            {
                properties.AfterProperties[FieldNames.IssuedDate] = SPUtility_FormatDate(DateTime.Today);
            }
        }

        private static string SPUtility_FormatDate(DateTime date)
        {
            return Microsoft.SharePoint.Utilities.SPUtility.CreateISO8601DateTimeFromSystemDateTime(date);
        }
    }

    /// <summary>Project site "Risks": critical risks notify the project director by mail.</summary>
    public class RiskReceiver : SPItemEventReceiver
    {
        public override void ItemAdded(SPItemEventProperties properties)
        {
            Notify(properties);
        }

        public override void ItemUpdated(SPItemEventProperties properties)
        {
            Notify(properties);
        }

        private static void Notify(SPItemEventProperties properties)
        {
            var item = properties.ListItem;
            if (!string.Equals(item[FieldNames.Severity] as string, RiskSeverity.Critical.ToString(), StringComparison.Ordinal))
            {
                return;
            }

            var director = properties.Web.Site.RootWeb.AllProperties["lucerne_director_email"] as string;
            if (string.IsNullOrEmpty(director))
            {
                return;
            }

            Microsoft.SharePoint.Utilities.SPUtility.SendEmail(
                properties.Web,
                false,
                false,
                director,
                "Critical risk on " + properties.Web.Title,
                "<p>A critical risk was recorded: <b>" + System.Web.HttpUtility.HtmlEncode(item.Title) + "</b></p><p><a href=\"" + properties.Web.Url + "/" + item.Url + "\">Open the risk register</a></p>");
        }
    }
}
