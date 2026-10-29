using System;
using System.Collections.Generic;

namespace Lucerne.ProjectSites.SharePoint
{
    /// <summary>
    /// Opens SharePoint sites. Implemented over the server object model (farm code: timer jobs, event receivers,
    /// application pages) and over CSOM (provider-hosted add-in, sync service, admin tool).
    /// </summary>
    public interface ISiteConnector
    {
        ISiteSession Open(string siteUrl);
    }

    public interface ISiteSession : IDisposable
    {
        string Url { get; }
        string Title { get; }

        bool ListExists(string title);
        IListGateway GetList(string title);

        string GetProperty(string key);
        void SetProperty(string key, string value);

        ISiteSecurity Security { get; }

        /// <summary>Sends pending changes. A no-op for the server object model.</summary>
        void Commit();
    }

    public interface IListGateway
    {
        string Title { get; }
        bool IsLibrary { get; }

        /// <summary>Runs the query and pages through the results (RowLimit is the page size).</summary>
        IEnumerable<ListItemData> Query(CamlQuery query);

        ListItemData GetById(int id);
        int Add(FieldValues values);

        /// <param name="systemUpdate">true keeps Modified/Editor and does not create a version (SystemUpdate).</param>
        void Update(int id, FieldValues values, bool systemUpdate);

        void Recycle(int id);

        int UploadFile(string fileName, byte[] content, FieldValues values, bool overwrite);
        byte[] OpenFile(int id);

        /// <summary>Moves a document to another library, keeping metadata. Returns the new server relative URL.</summary>
        string MoveFile(int id, string destinationLibrary);
    }

    public interface ISiteSecurity
    {
        void EnsureGroup(string name, string permissionLevel, string description);
        IReadOnlyCollection<string> GetGroupMembers(string groupName);
        void AddToGroup(string groupName, string login);
        void RemoveFromGroup(string groupName, string login);
    }

    public class SiteCreationRequest
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string WebTemplate { get; set; }
        public int Lcid { get; set; }
        public string OwnerLogin { get; set; }
        public string SecondaryOwnerLogin { get; set; }
        public string ContentDatabase { get; set; }
        public IDictionary<string, string> Properties { get; set; }
    }

    public interface ISiteProvisioner
    {
        bool SiteExists(string url);
        void CreateSiteCollection(SiteCreationRequest request);
    }

    public class SharePointException : Exception
    {
        public SharePointException(string message)
            : base(message)
        {
        }

        public SharePointException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>True for throttling (429/503) and save conflicts that may succeed on retry.</summary>
        public bool Transient { get; set; }
    }
}
