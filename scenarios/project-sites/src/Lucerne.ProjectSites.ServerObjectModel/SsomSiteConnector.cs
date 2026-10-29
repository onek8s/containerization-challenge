using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SharePoint;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.ServerObjectModel
{
    /// <summary>
    /// Server object model connector for farm code. Timer jobs run as the farm account; pages and receivers
    /// open sites elevated (application pool account) when they touch the hub.
    /// </summary>
    public class SsomSiteConnector : ISiteConnector
    {
        private readonly bool _elevated;

        public SsomSiteConnector(bool elevated)
        {
            _elevated = elevated;
        }

        public ISiteSession Open(string siteUrl)
        {
            SPSite site = null;
            if (_elevated)
            {
                SPSecurity.RunWithElevatedPrivileges(() => site = new SPSite(siteUrl));
            }
            else
            {
                site = new SPSite(siteUrl);
            }

            try
            {
                var web = site.OpenWeb();
                web.AllowUnsafeUpdates = true;
                return new SsomSiteSession(site, web, true);
            }
            catch
            {
                site.Dispose();
                throw;
            }
        }

        /// <summary>Wraps an SPWeb owned by SharePoint (SPContext, event properties); it is not disposed.</summary>
        public static ISiteSession Wrap(SPWeb web)
        {
            return new SsomSiteSession(web.Site, web, false);
        }
    }

    internal sealed class SsomSiteSession : ISiteSession
    {
        private readonly SPSite _site;
        private readonly SPWeb _web;
        private readonly bool _owns;
        private readonly Dictionary<string, SsomListGateway> _lists = new Dictionary<string, SsomListGateway>(StringComparer.OrdinalIgnoreCase);

        public SsomSiteSession(SPSite site, SPWeb web, bool owns)
        {
            _site = site;
            _web = web;
            _owns = owns;
            Security = new SsomSiteSecurity(web);
        }

        public string Url
        {
            get { return _web.Url; }
        }

        public string Title
        {
            get { return _web.Title; }
        }

        public ISiteSecurity Security { get; }

        public bool ListExists(string title)
        {
            return _web.Lists.TryGetList(title) != null;
        }

        public IListGateway GetList(string title)
        {
            SsomListGateway gateway;
            if (!_lists.TryGetValue(title, out gateway))
            {
                var list = _web.Lists.TryGetList(title);
                if (list == null)
                {
                    throw new SharePointException("List '" + title + "' does not exist in " + _web.Url);
                }

                gateway = new SsomListGateway(_web, list);
                _lists[title] = gateway;
            }

            return gateway;
        }

        public string GetProperty(string key)
        {
            var value = _web.AllProperties[key];
            return value == null ? null : value.ToString();
        }

        public void SetProperty(string key, string value)
        {
            _web.AllProperties[key] = value;
            try
            {
                _web.Update();
            }
            catch (SPException ex)
            {
                // Concurrent property bag updates fail with a save conflict; callers may retry.
                throw new SharePointException(ex.Message, ex) { Transient = true };
            }
        }

        public void Commit()
        {
        }

        public void Dispose()
        {
            if (_owns)
            {
                _web.Dispose();
                _site.Dispose();
            }
        }
    }

    internal sealed class SsomSiteSecurity : ISiteSecurity
    {
        private readonly SPWeb _web;

        public SsomSiteSecurity(SPWeb web)
        {
            _web = web;
        }

        public void EnsureGroup(string name, string permissionLevel, string description)
        {
            if (_web.SiteGroups.OfType<SPGroup>().Any(g => g.Name == name))
            {
                return;
            }

            _web.SiteGroups.Add(name, _web.Site.Owner, null, description);
            var group = _web.SiteGroups[name];
            var assignment = new SPRoleAssignment(group);
            assignment.RoleDefinitionBindings.Add(_web.RoleDefinitions[permissionLevel]);
            _web.RoleAssignments.Add(assignment);
        }

        public IReadOnlyCollection<string> GetGroupMembers(string groupName)
        {
            return _web.SiteGroups[groupName].Users.OfType<SPUser>().Select(u => u.LoginName).ToList();
        }

        public void AddToGroup(string groupName, string login)
        {
            var user = _web.EnsureUser(login);
            _web.SiteGroups[groupName].AddUser(user);
        }

        public void RemoveFromGroup(string groupName, string login)
        {
            _web.SiteGroups[groupName].Users.Remove(login);
        }
    }
}
