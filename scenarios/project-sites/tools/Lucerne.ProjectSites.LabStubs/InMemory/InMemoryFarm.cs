using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.LabStubs.InMemory
{
    /// <summary>
    /// In-memory stand-in for the SharePoint farm: site collections with lists, libraries, property bags and
    /// groups. Implements the same abstraction the farm code (server object model) and the services (CSOM) use.
    /// </summary>
    public class InMemoryFarm : ISiteConnector, ISiteProvisioner
    {
        private readonly ConcurrentDictionary<string, InMemorySite> _sites = new ConcurrentDictionary<string, InMemorySite>(StringComparer.OrdinalIgnoreCase);

        public InMemoryFarm(string currentUserLogin = "i:0#.w|corp\\svc-ps-farm")
        {
            CurrentUserLogin = currentUserLogin;
        }

        public string CurrentUserLogin { get; set; }

        public IEnumerable<InMemorySite> Sites => _sites.Values;

        public static string Normalize(string url) => url.TrimEnd('/');

        public InMemorySite CreateSite(string url, string title, IEnumerable<ListDefinition> lists)
        {
            var site = new InMemorySite(this, Normalize(url), title);
            foreach (var l in lists)
            {
                site.CreateList(l.Title, l.IsLibrary);
            }

            if (!_sites.TryAdd(site.Url, site))
            {
                throw new SharePointException("A site already exists at " + url);
            }

            return site;
        }

        public InMemorySite CreateHub(string url)
        {
            return CreateSite(url, "Lucerne Projects", ProjectSiteSchema.HubLists());
        }

        public InMemorySite Get(string url)
        {
            InMemorySite site;
            if (!_sites.TryGetValue(Normalize(url), out site))
            {
                throw new SharePointException("404 NOT FOUND: no site at " + url);
            }

            return site;
        }

        public ISiteSession Open(string siteUrl) => new InMemorySession(Get(siteUrl));

        public bool SiteExists(string url) => _sites.ContainsKey(Normalize(url));

        /// <summary>Mimics the Lucerne project web template: project lists + discipline libraries from the property bag.</summary>
        public void CreateSiteCollection(SiteCreationRequest request)
        {
            if (request.WebTemplate != TemplateNames.ProjectWebTemplate)
            {
                throw new SharePointException("Unknown web template " + request.WebTemplate);
            }

            string disciplines = null;
            request.Properties?.TryGetValue("lucerne_disciplines", out disciplines);
            var site = CreateSite(request.Url, request.Title, ProjectSiteSchema.ProjectLists((disciplines ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)));
            site.Properties["lucerne_disciplines"] = disciplines;
            site.Properties["lucerne_contentdatabase"] = request.ContentDatabase;
            site.Properties[WebProperties.TemplateVersion] = TemplateNames.CurrentVersion.ToString();
            site.Owner = request.OwnerLogin;
        }
    }

    public class InMemorySite
    {
        private readonly Dictionary<string, InMemoryList> _lists = new Dictionary<string, InMemoryList>(StringComparer.OrdinalIgnoreCase);

        public InMemorySite(InMemoryFarm farm, string url, string title)
        {
            Farm = farm;
            Url = url;
            Title = title;
            Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Groups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            GroupRoles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public InMemoryFarm Farm { get; }
        public string Url { get; }
        public string Title { get; }
        public string Owner { get; set; }
        public Dictionary<string, string> Properties { get; }
        public Dictionary<string, HashSet<string>> Groups { get; }
        public Dictionary<string, string> GroupRoles { get; }
        public IEnumerable<InMemoryList> Lists => _lists.Values;

        public string ServerRelativeUrl => new Uri(Url).AbsolutePath.TrimEnd('/');

        public InMemoryList CreateList(string title, bool library)
        {
            var list = new InMemoryList(this, title, library);
            _lists[title] = list;
            return list;
        }

        public bool HasList(string title) => _lists.ContainsKey(title);

        public InMemoryList List(string title)
        {
            InMemoryList list;
            if (!_lists.TryGetValue(title, out list))
            {
                throw new SharePointException("List '" + title + "' does not exist at site with URL '" + Url + "'.");
            }

            return list;
        }
    }

    public class InMemoryList : IListGateway
    {
        private readonly object _gate = new object();
        private readonly SortedDictionary<int, FieldValues> _items = new SortedDictionary<int, FieldValues>();
        private readonly Dictionary<int, byte[]> _files = new Dictionary<int, byte[]>();
        private int _nextId = 1;

        public InMemoryList(InMemorySite site, string title, bool library)
        {
            Site = site;
            Title = title;
            IsLibrary = library;
            RecycleBin = new List<FieldValues>();
        }

        public InMemorySite Site { get; }
        public string Title { get; }
        public bool IsLibrary { get; }
        public List<FieldValues> RecycleBin { get; }
        public int Count { get { lock (_gate) { return _items.Count; } } }

        public string RootFolderUrl => Site.ServerRelativeUrl + "/" + (IsLibrary ? Title.Replace(" ", string.Empty) : "Lists/" + Title.Replace(" ", string.Empty));

        public IEnumerable<ListItemData> Query(CamlQuery query)
        {
            List<ListItemData> snapshot;
            lock (_gate)
            {
                snapshot = _items.Select(p => new ListItemData(p.Key, p.Value)).ToList();
            }

            var result = CamlEvaluator.Apply(snapshot, query?.Query, Site.Farm.CurrentUserLogin);
            return query != null && query.MaxItems > 0 ? result.Take(query.MaxItems) : result;
        }

        public ListItemData GetById(int id)
        {
            lock (_gate)
            {
                FieldValues v;
                if (!_items.TryGetValue(id, out v))
                {
                    throw new SharePointException("Item does not exist. It may have been deleted by another user.");
                }

                return new ListItemData(id, v);
            }
        }

        public int Add(FieldValues values)
        {
            lock (_gate)
            {
                var id = _nextId++;
                var v = new FieldValues(values);
                v[FieldNames.Id] = id;
                v[FieldNames.Created] = DateTime.UtcNow;
                v[FieldNames.Modified] = DateTime.UtcNow;
                v[FieldNames.Author] = new UserValue(Site.Farm.CurrentUserLogin);
                if (!IsLibrary)
                {
                    v[FieldNames.FileRef] = RootFolderUrl + "/" + id + "_.000";
                }

                _items[id] = v;
                return id;
            }
        }

        public void Update(int id, FieldValues values, bool systemUpdate)
        {
            lock (_gate)
            {
                FieldValues v;
                if (!_items.TryGetValue(id, out v))
                {
                    throw new SharePointException("Item does not exist. It may have been deleted by another user.");
                }

                foreach (var p in values)
                {
                    v[p.Key] = p.Value;
                }

                if (!systemUpdate)
                {
                    v[FieldNames.Modified] = DateTime.UtcNow;
                }
            }
        }

        public void Recycle(int id)
        {
            lock (_gate)
            {
                FieldValues v;
                if (_items.TryGetValue(id, out v))
                {
                    RecycleBin.Add(v);
                    _items.Remove(id);
                    _files.Remove(id);
                }
            }
        }

        public int UploadFile(string fileName, byte[] content, FieldValues values, bool overwrite)
        {
            if (!IsLibrary)
            {
                throw new SharePointException(Title + " is not a document library");
            }

            var url = RootFolderUrl + "/" + fileName;
            lock (_gate)
            {
                var existing = _items.FirstOrDefault(p => string.Equals(p.Value[FieldNames.FileRef] as string, url, StringComparison.OrdinalIgnoreCase));
                if (existing.Value != null && !overwrite)
                {
                    throw new SharePointException("A file with the name " + url + " already exists.");
                }
            }

            var v = new FieldValues(values ?? new FieldValues());
            v[FieldNames.FileRef] = url;
            v[FieldNames.FileLeafRef] = fileName;
            if (!v.ContainsKey(FieldNames.Title))
            {
                v[FieldNames.Title] = System.IO.Path.GetFileNameWithoutExtension(fileName);
            }

            var id = Add(v);
            lock (_gate)
            {
                _files[id] = content;
            }

            return id;
        }

        public byte[] OpenFile(int id)
        {
            lock (_gate)
            {
                byte[] content;
                if (!_files.TryGetValue(id, out content))
                {
                    throw new SharePointException("File not found for item " + id);
                }

                return content;
            }
        }

        public string MoveFile(int id, string destinationLibrary)
        {
            var target = Site.List(destinationLibrary);
            ListItemData item;
            byte[] content;
            lock (_gate)
            {
                item = GetById(id);
                content = _files[id];
                _items.Remove(id);
                _files.Remove(id);
            }

            var values = new FieldValues(item.Values);
            values.Remove(FieldNames.Id);
            var newId = target.UploadFile(item.GetString(FieldNames.FileLeafRef), content, values, true);
            return target.GetById(newId).GetString(FieldNames.FileRef);
        }
    }

    internal sealed class InMemorySession : ISiteSession, ISiteSecurity
    {
        private readonly InMemorySite _site;

        public InMemorySession(InMemorySite site)
        {
            _site = site;
        }

        public string Url => _site.Url;
        public string Title => _site.Title;
        public ISiteSecurity Security => this;

        public bool ListExists(string title) => _site.HasList(title);
        public IListGateway GetList(string title) => _site.List(title);

        public string GetProperty(string key)
        {
            lock (_site.Properties)
            {
                string v;
                return _site.Properties.TryGetValue(key, out v) ? v : null;
            }
        }

        public void SetProperty(string key, string value)
        {
            lock (_site.Properties)
            {
                _site.Properties[key] = value;
            }
        }

        public void Commit()
        {
        }

        public void Dispose()
        {
        }

        public void EnsureGroup(string name, string permissionLevel, string description)
        {
            lock (_site.Groups)
            {
                if (!_site.Groups.ContainsKey(name))
                {
                    _site.Groups[name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _site.GroupRoles[name] = permissionLevel;
                }
            }
        }

        public IReadOnlyCollection<string> GetGroupMembers(string groupName)
        {
            lock (_site.Groups)
            {
                return Group(groupName).ToList();
            }
        }

        public void AddToGroup(string groupName, string login)
        {
            lock (_site.Groups)
            {
                Group(groupName).Add(login);
            }
        }

        public void RemoveFromGroup(string groupName, string login)
        {
            lock (_site.Groups)
            {
                Group(groupName).Remove(login);
            }
        }

        private HashSet<string> Group(string name)
        {
            HashSet<string> members;
            if (!_site.Groups.TryGetValue(name, out members))
            {
                throw new SharePointException("Group cannot be found: " + name);
            }

            return members;
        }
    }
}
