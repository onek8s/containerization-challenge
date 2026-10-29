using System;
using System.Linq;
using Lucerne.ProjectSites.SharePoint;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;

namespace Lucerne.ProjectSites.ServerObjectModel
{
    /// <summary>Creates project site collections in the projects web application (farm account only).</summary>
    public class SsomSiteProvisioner : ISiteProvisioner
    {
        private readonly SPWebApplication _webApplication;

        public SsomSiteProvisioner(SPWebApplication webApplication)
        {
            _webApplication = webApplication;
        }

        public bool SiteExists(string url)
        {
            return SPSite.Exists(new Uri(url));
        }

        public void CreateSiteCollection(SiteCreationRequest request)
        {
            var database = _webApplication.ContentDatabases.OfType<SPContentDatabase>()
                .FirstOrDefault(d => string.Equals(d.Name, request.ContentDatabase, StringComparison.OrdinalIgnoreCase));

            if (database == null)
            {
                // New project year: create the content database on the default database server.
                database = _webApplication.ContentDatabases.Add(
                    SPWebService.ContentService.DefaultDatabaseInstance.NormalizedDataSource,
                    request.ContentDatabase, null, null, 9000, 10000, 0);
            }

            using (var site = database.Sites.Add(
                request.Url,
                request.Title,
                request.Description,
                (uint)request.Lcid,
                request.WebTemplate,
                request.OwnerLogin,
                string.Empty,
                string.Empty,
                request.SecondaryOwnerLogin,
                string.Empty,
                string.Empty))
            {
                site.RootWeb.AllProperties["lucerne_createdby"] = Environment.UserDomainName + "\\" + Environment.UserName;
                site.RootWeb.Update();
            }
        }
    }
}
