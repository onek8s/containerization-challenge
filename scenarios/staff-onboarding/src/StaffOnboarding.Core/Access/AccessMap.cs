using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace StaffOnboarding.Core.Access
{
    /// <summary>Role based access map maintained by IT (access-map.xml).</summary>
    public class AccessMap
    {
        private readonly Dictionary<string, DepartmentAccess> _departments;
        private readonly Dictionary<string, GroupGrant> _knownGroups;

        public AccessMap(string usersOu, string externalOu, string disabledOu, string homeRoot, string homeDrive,
            IEnumerable<GroupGrant> baseline, IEnumerable<GroupGrant> externalBaseline, IEnumerable<DepartmentAccess> departments,
            IEnumerable<GroupGrant> requestable)
        {
            UsersOu = usersOu;
            ExternalOu = externalOu;
            DisabledOu = disabledOu;
            HomeRoot = homeRoot;
            HomeDrive = homeDrive;
            Baseline = baseline.ToList();
            ExternalBaseline = externalBaseline.ToList();
            Requestable = requestable.ToList();
            _departments = departments.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);

            _knownGroups = new Dictionary<string, GroupGrant>(StringComparer.OrdinalIgnoreCase);
            var all = Baseline.Concat(ExternalBaseline).Concat(Requestable)
                .Concat(_departments.Values.SelectMany(d => d.Groups.Concat(d.Roles.Values.SelectMany(r => r.Groups))));
            foreach (var g in all)
            {
                GroupGrant existing;
                if (!_knownGroups.TryGetValue(g.Name, out existing) || (g.Privileged && !existing.Privileged))
                    _knownGroups[g.Name] = g;
            }
        }

        public string UsersOu { get; private set; }
        public string ExternalOu { get; private set; }
        public string DisabledOu { get; private set; }
        public string HomeRoot { get; private set; }
        public string HomeDrive { get; private set; }
        public IReadOnlyList<GroupGrant> Baseline { get; private set; }
        public IReadOnlyList<GroupGrant> ExternalBaseline { get; private set; }
        public IReadOnlyList<GroupGrant> Requestable { get; private set; }

        public IEnumerable<DepartmentAccess> Departments
        {
            get { return _departments.Values.OrderBy(d => d.Code); }
        }

        public DepartmentAccess FindDepartment(string code)
        {
            DepartmentAccess d;
            return code != null && _departments.TryGetValue(code, out d) ? d : null;
        }

        /// <summary>Groups that appear anywhere in the map. Other groups are not changed by onboarding.</summary>
        public bool IsManaged(string group)
        {
            return group != null && _knownGroups.ContainsKey(group);
        }

        public bool IsPrivileged(string group)
        {
            GroupGrant g;
            return group != null && _knownGroups.TryGetValue(group, out g) && g.Privileged;
        }

        public bool IsRequestable(string group)
        {
            return Requestable.Any(g => string.Equals(g.Name, group, StringComparison.OrdinalIgnoreCase));
        }

        public static AccessMap Load(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return Parse(XDocument.Load(stream));
            }
        }

        public static AccessMap Parse(XDocument doc)
        {
            var root = doc.Root;
            if (root == null || root.Name.LocalName != "accessMap")
                throw new FormatException("Root element must be <accessMap>.");

            var ous = Required(root, "ous");
            var home = Required(root, "homeFolders");

            var departments = root.Elements("department").Select(d => new DepartmentAccess(
                Attr(d, "code"),
                (string)d.Attribute("name") ?? Attr(d, "code"),
                Attr(d, "ou"),
                (string)d.Attribute("share"),
                Grants(d),
                d.Elements("role").Select(r => new RoleAccess(Attr(r, "code"), (string)r.Attribute("title"), Grants(r)))));

            return new AccessMap(
                Attr(ous, "users"),
                Attr(ous, "external"),
                Attr(ous, "disabled"),
                Attr(home, "root"),
                (string)home.Attribute("drive") ?? "H:",
                Grants(root.Element("baseline")),
                Grants(root.Element("externalBaseline")),
                departments,
                Grants(root.Element("requestable")));
        }

        private static IEnumerable<GroupGrant> Grants(XElement parent)
        {
            if (parent == null) return Enumerable.Empty<GroupGrant>();
            return parent.Elements("group")
                .Select(g => new GroupGrant(Attr(g, "name"), (bool?)g.Attribute("privileged") ?? false))
                .ToList();
        }

        private static XElement Required(XElement parent, string name)
        {
            var e = parent.Element(name);
            if (e == null) throw new FormatException("Missing <" + name + "> element.");
            return e;
        }

        private static string Attr(XElement e, string name)
        {
            var value = (string)e.Attribute(name);
            if (string.IsNullOrWhiteSpace(value))
                throw new FormatException("<" + e.Name.LocalName + "> is missing attribute '" + name + "'.");
            return value.Trim();
        }
    }

    public class GroupGrant
    {
        public GroupGrant(string name, bool privileged)
        {
            Name = name;
            Privileged = privileged;
        }

        public string Name { get; private set; }

        /// <summary>Privileged groups need an IT security approval.</summary>
        public bool Privileged { get; private set; }
    }

    public class DepartmentAccess
    {
        public DepartmentAccess(string code, string name, string ou, string share, IEnumerable<GroupGrant> groups, IEnumerable<RoleAccess> roles)
        {
            Code = code;
            Name = name;
            Ou = ou;
            Share = share;
            Groups = groups.ToList();
            Roles = roles.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);
        }

        public string Code { get; private set; }
        public string Name { get; private set; }
        public string Ou { get; private set; }
        public string Share { get; private set; }
        public IReadOnlyList<GroupGrant> Groups { get; private set; }
        public IReadOnlyDictionary<string, RoleAccess> Roles { get; private set; }

        public RoleAccess FindRole(string code)
        {
            RoleAccess r;
            return code != null && Roles.TryGetValue(code, out r) ? r : null;
        }
    }

    public class RoleAccess
    {
        public RoleAccess(string code, string title, IEnumerable<GroupGrant> groups)
        {
            Code = code;
            Title = title;
            Groups = groups.ToList();
        }

        public string Code { get; private set; }
        public string Title { get; private set; }
        public IReadOnlyList<GroupGrant> Groups { get; private set; }
    }
}
