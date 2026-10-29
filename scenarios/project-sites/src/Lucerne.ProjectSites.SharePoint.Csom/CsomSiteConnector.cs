using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.SharePoint.Client;
using Polly;

namespace Lucerne.ProjectSites.SharePoint.Csom
{
    public class CsomSiteConnector : ISiteConnector
    {
        private readonly IClientContextFactory _factory;

        public CsomSiteConnector(IClientContextFactory factory)
        {
            _factory = factory;
        }

        public ISiteSession Open(string siteUrl)
        {
            var ctx = _factory.Create(siteUrl);
            var web = ctx.Web;
            ctx.Load(web, w => w.Title, w => w.Url, w => w.ServerRelativeUrl, w => w.AllProperties);
            Csom.Execute(ctx);
            return new CsomSiteSession(ctx, web);
        }
    }

    internal static class Csom
    {
        /// <summary>ExecuteQuery with retries for throttling (429/503) and save conflicts.</summary>
        public static void Execute(ClientRuntimeContext ctx)
        {
            Policy
                .Handle<WebException>(IsThrottled)
                .Or<ServerException>(ex => ex.ServerErrorTypeName == "Microsoft.SharePoint.SPException" && ex.Message.IndexOf("Save Conflict", StringComparison.OrdinalIgnoreCase) >= 0)
                .WaitAndRetry(5, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)))
                .Execute(ctx.ExecuteQuery);
        }

        private static bool IsThrottled(WebException ex)
        {
            var response = ex.Response as HttpWebResponse;
            return response != null && ((int)response.StatusCode == 429 || response.StatusCode == HttpStatusCode.ServiceUnavailable);
        }
    }

    internal sealed class CsomSiteSession : ISiteSession
    {
        private readonly ClientContext _ctx;
        private readonly Web _web;
        private readonly Dictionary<string, CsomListGateway> _lists = new Dictionary<string, CsomListGateway>(StringComparer.OrdinalIgnoreCase);
        private readonly UserCache _users;
        private bool _dirty;

        public CsomSiteSession(ClientContext ctx, Web web)
        {
            _ctx = ctx;
            _web = web;
            _users = new UserCache(ctx, web);
            Security = new CsomSiteSecurity(ctx, web);
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
            var lists = _web.Lists;
            _ctx.Load(lists, l => l.Include(x => x.Title).Where(x => x.Title == title));
            Csom.Execute(_ctx);
            return lists.Any();
        }

        public IListGateway GetList(string title)
        {
            CsomListGateway gateway;
            if (!_lists.TryGetValue(title, out gateway))
            {
                var list = _web.Lists.GetByTitle(title);
                _ctx.Load(list, l => l.Title, l => l.BaseType, l => l.RootFolder.ServerRelativeUrl);
                Csom.Execute(_ctx);
                gateway = new CsomListGateway(_ctx, _web, list, _users);
                _lists[title] = gateway;
            }

            return gateway;
        }

        public string GetProperty(string key)
        {
            object value;
            return _web.AllProperties.FieldValues.TryGetValue(key, out value) && value != null ? value.ToString() : null;
        }

        public void SetProperty(string key, string value)
        {
            _web.AllProperties[key] = value;
            _web.Update();
            _dirty = true;
        }

        public void Commit()
        {
            if (_dirty || _ctx.HasPendingRequest)
            {
                Csom.Execute(_ctx);
                _dirty = false;
            }
        }

        public void Dispose()
        {
            _ctx.Dispose();
        }
    }

    /// <summary>Resolves person field ids to claims logins (FieldUserValue carries only id and display name).</summary>
    internal sealed class UserCache
    {
        private readonly ClientContext _ctx;
        private readonly Web _web;
        private readonly Dictionary<int, string> _logins = new Dictionary<int, string>();

        public UserCache(ClientContext ctx, Web web)
        {
            _ctx = ctx;
            _web = web;
        }

        public void Prefetch(IEnumerable<int> ids)
        {
            var missing = ids.Where(id => id > 0 && !_logins.ContainsKey(id)).Distinct().ToList();
            if (missing.Count == 0)
            {
                return;
            }

            var users = missing.Select(id => new { id, user = _web.SiteUsers.GetById(id) }).ToList();
            foreach (var u in users)
            {
                _ctx.Load(u.user, x => x.LoginName);
            }

            try
            {
                Csom.Execute(_ctx);
                foreach (var u in users)
                {
                    _logins[u.id] = u.user.LoginName;
                }
            }
            catch (ServerException)
            {
                // A removed user fails the whole batch; fall back to one by one.
                foreach (var id in missing)
                {
                    try
                    {
                        var user = _web.SiteUsers.GetById(id);
                        _ctx.Load(user, x => x.LoginName);
                        Csom.Execute(_ctx);
                        _logins[id] = user.LoginName;
                    }
                    catch (ServerException)
                    {
                        _logins[id] = null;
                    }
                }
            }
        }

        public string Login(int id)
        {
            string login;
            return _logins.TryGetValue(id, out login) ? login : null;
        }
    }
}
