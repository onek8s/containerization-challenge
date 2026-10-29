using System;
using System.Collections.Generic;
using System.Linq;
using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Core.Access
{
    public class AccessPlanner
    {
        private const string PrimaryGroup = "Domain Users";
        private readonly AccessMap _map;

        public AccessPlanner(AccessMap map)
        {
            if (map == null) throw new ArgumentNullException("map");
            _map = map;
        }

        public AccessPlan PlanJoiner(PositionDetails position, IEnumerable<string> additionalGroups)
        {
            var plan = new AccessPlan();
            var department = RequireDepartment(position, plan);

            var external = position.EmploymentType == EmploymentType.Contractor;
            plan.TargetOu = external ? _map.ExternalOu : department.Ou;
            plan.DepartmentShare = department.Share;

            var groups = RoleGroups(position, department, plan).ToList();
            groups.AddRange(external ? _map.ExternalBaseline : _map.Baseline);
            AddRequested(additionalGroups, groups, plan);

            plan.AddGroups = Distinct(groups.Select(g => g.Name));
            plan.RequiresITApproval = plan.RequiresITApproval || groups.Any(g => g.Privileged);
            return plan;
        }

        /// <summary>
        /// New role groups are added on the effective date; managed groups of the old role are removed
        /// after the handover period. Unmanaged groups are left alone and reported.
        /// </summary>
        public AccessPlan PlanMover(IEnumerable<string> currentGroups, PositionDetails newPosition, IEnumerable<string> additionalGroups, int handoverDays)
        {
            var plan = new AccessPlan();
            var department = RequireDepartment(newPosition, plan);
            var current = new HashSet<string>(currentGroups ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            var external = newPosition.EmploymentType == EmploymentType.Contractor;
            plan.TargetOu = external ? _map.ExternalOu : department.Ou;
            plan.DepartmentShare = department.Share;

            var target = RoleGroups(newPosition, department, plan).ToList();
            target.AddRange(external ? _map.ExternalBaseline : _map.Baseline);
            AddRequested(additionalGroups, target, plan);
            var targetNames = new HashSet<string>(target.Select(g => g.Name), StringComparer.OrdinalIgnoreCase);

            plan.AddGroups = Distinct(target.Where(g => !current.Contains(g.Name)).Select(g => g.Name));
            plan.RequiresITApproval = plan.RequiresITApproval ||
                target.Any(g => g.Privileged && !current.Contains(g.Name));

            var obsolete = Distinct(current.Where(g => _map.IsManaged(g) && !targetNames.Contains(g)));
            var immediate = obsolete.Where(g => handoverDays == 0 || _map.IsPrivileged(g)).ToList();
            plan.RemoveGroups = immediate;
            plan.DeferredRemoveGroups = obsolete.Except(immediate, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var g in current.Where(g => !_map.IsManaged(g) && !IsPrimary(g)).OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
                plan.Warnings.Add("Group '" + g + "' is not in the access map and is kept. Review it with the new manager.");

            return plan;
        }

        public AccessPlan PlanLeaver(IEnumerable<string> currentGroups)
        {
            return new AccessPlan
            {
                TargetOu = _map.DisabledOu,
                RemoveGroups = Distinct((currentGroups ?? Enumerable.Empty<string>()).Where(g => !IsPrimary(g)))
            };
        }

        private DepartmentAccess RequireDepartment(PositionDetails position, AccessPlan plan)
        {
            if (position == null) throw new ArgumentNullException("position");
            var department = _map.FindDepartment(position.Department);
            if (department == null)
                throw new ArgumentException("Department '" + position.Department + "' is not in the access map.");
            return department;
        }

        private static IEnumerable<GroupGrant> RoleGroups(PositionDetails position, DepartmentAccess department, AccessPlan plan)
        {
            foreach (var g in department.Groups) yield return g;

            var role = department.FindRole(position.JobRole);
            if (role == null)
            {
                plan.Warnings.Add("Job role '" + position.JobRole + "' has no access profile in " + department.Code +
                                  ". Only department groups are granted.");
                plan.RequiresITApproval = true;
                yield break;
            }
            foreach (var g in role.Groups) yield return g;
        }

        private void AddRequested(IEnumerable<string> additionalGroups, List<GroupGrant> groups, AccessPlan plan)
        {
            foreach (var name in (additionalGroups ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var trimmed = name.Trim();
                if (!_map.IsRequestable(trimmed))
                {
                    plan.Warnings.Add("Group '" + trimmed + "' cannot be requested and was ignored.");
                    continue;
                }
                groups.Add(new GroupGrant(trimmed, _map.IsPrivileged(trimmed)));
                plan.RequiresITApproval = true;
            }
        }

        private static bool IsPrimary(string group)
        {
            return string.Equals(group, PrimaryGroup, StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> Distinct(IEnumerable<string> names)
        {
            return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
