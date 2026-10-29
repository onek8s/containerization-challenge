using System;
using Lucerne.ProjectSites.Contracts.Schema;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;

namespace Lucerne.ProjectSites.Farm.Common
{
    /// <summary>Settings stored in the property bag of the projects web application (set by Install-FarmSolution).</summary>
    public static class FarmSettings
    {
        public const string RecordsManagerGroup = "lucerne.projectsites.recordsmanagers";

        public static string HubUrl(SPWebApplication webApplication)
        {
            var value = webApplication.Properties[WebProperties.WebAppHubUrl] as string;
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException("Web application property " + WebProperties.WebAppHubUrl + " is not set on " + webApplication.Name);
            }

            return value;
        }

        public static string HubUrl(SPWeb web)
        {
            var fromWeb = web.AllProperties[WebProperties.HubUrl] as string;
            return string.IsNullOrEmpty(fromWeb) ? HubUrl(web.Site.WebApplication) : fromWeb;
        }

        public static string ProjectNumber(SPWeb web)
        {
            return web.Site.RootWeb.AllProperties[WebProperties.ProjectNumber] as string;
        }

        public static string WebApplicationUrl(SPWebApplication webApplication)
        {
            return webApplication.GetResponseUri(SPUrlZone.Default).AbsoluteUri.TrimEnd('/');
        }
    }
}
