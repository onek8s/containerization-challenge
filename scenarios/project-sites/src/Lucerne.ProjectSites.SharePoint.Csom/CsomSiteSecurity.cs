using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SharePoint.Client;

namespace Lucerne.ProjectSites.SharePoint.Csom
{
    internal sealed class CsomSiteSecurity : ISiteSecurity
    {
        private readonly ClientContext _ctx;
        private readonly Web _web;

        public CsomSiteSecurity(ClientContext ctx, Web web)
        {
            _ctx = ctx;
            _web = web;
        }

        public void EnsureGroup(string name, string permissionLevel, string description)
        {
            var groups = _web.SiteGroups;
            _ctx.Load(groups, g => g.Include(x => x.Title).Where(x => x.Title == name));
            Csom.Execute(_ctx);
            if (groups.Any())
            {
                return;
            }

            var group = groups.Add(new GroupCreationInformation { Title = name, Description = description });
            var bindings = new RoleDefinitionBindingCollection(_ctx) { _web.RoleDefinitions.GetByName(permissionLevel) };
            _web.RoleAssignments.Add(group, bindings);
            Csom.Execute(_ctx);
        }

        public IReadOnlyCollection<string> GetGroupMembers(string groupName)
        {
            var users = _web.SiteGroups.GetByName(groupName).Users;
            _ctx.Load(users, u => u.Include(x => x.LoginName));
            Csom.Execute(_ctx);
            return users.Select(u => u.LoginName).ToList();
        }

        public void AddToGroup(string groupName, string login)
        {
            var user = _web.EnsureUser(login);
            _web.SiteGroups.GetByName(groupName).Users.AddUser(user);
            Csom.Execute(_ctx);
        }

        public void RemoveFromGroup(string groupName, string login)
        {
            _web.SiteGroups.GetByName(groupName).Users.RemoveByLoginName(login);
            Csom.Execute(_ctx);
        }
    }
}
