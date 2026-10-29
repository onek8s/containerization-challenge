using System;
using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.Domain;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;

namespace Lucerne.ProjectSites.Claims
{
    /// <summary>
    /// Augments Windows claims users with office, discipline and all-staff claims, and lets the people picker
    /// find them ("Office: Zurich", "Discipline: Structural", "All Lucerne staff"). Project sites grant read
    /// access to the all-staff claim; discipline leads use the discipline claims for review groups.
    /// </summary>
    public class LucerneClaimProvider : SPClaimProvider
    {
        public const string ProviderInternalName = "LucerneClaims";
        public const string ProviderDisplayName = "Lucerne Engineering";
        public const string OfficeClaimType = "http://schemas.lucerne-eng.example/claims/office";
        public const string DisciplineClaimType = "http://schemas.lucerne-eng.example/claims/discipline";
        public const string StaffClaimType = "http://schemas.lucerne-eng.example/claims/staff";
        public const string StaffClaimValue = "allstaff";

        private static readonly string EntityType = SPClaimEntityTypes.FormsRole;

        public LucerneClaimProvider(string displayName)
            : base(displayName)
        {
        }

        public override string Name
        {
            get { return ProviderInternalName; }
        }

        public override bool SupportsEntityInformation
        {
            get { return true; }
        }

        public override bool SupportsHierarchy
        {
            get { return true; }
        }

        public override bool SupportsResolve
        {
            get { return true; }
        }

        public override bool SupportsSearch
        {
            get { return true; }
        }

        protected override void FillClaimTypes(List<string> claimTypes)
        {
            claimTypes.Add(OfficeClaimType);
            claimTypes.Add(DisciplineClaimType);
            claimTypes.Add(StaffClaimType);
        }

        protected override void FillClaimValueTypes(List<string> claimValueTypes)
        {
            claimValueTypes.Add(System.Security.Claims.ClaimValueTypes.String);
            claimValueTypes.Add(System.Security.Claims.ClaimValueTypes.String);
            claimValueTypes.Add(System.Security.Claims.ClaimValueTypes.String);
        }

        protected override void FillEntityTypes(List<string> entityTypes)
        {
            entityTypes.Add(EntityType);
        }

        protected override void FillClaimsForEntity(Uri context, SPClaim entity, List<SPClaim> claims)
        {
            if (entity == null || !string.Equals(entity.ClaimType, SPClaimTypes.UserLogonName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Windows users: the value is DOMAIN\account
            var cache = DirectoryCache.Local;
            string office, discipline, displayName;
            if (cache == null || !cache.TryGet(entity.Value, out office, out discipline, out displayName))
            {
                return;
            }

            claims.Add(CreateClaim(StaffClaimType, StaffClaimValue, System.Security.Claims.ClaimValueTypes.String));
            if (!string.IsNullOrEmpty(office))
            {
                claims.Add(CreateClaim(OfficeClaimType, office, System.Security.Claims.ClaimValueTypes.String));
            }

            if (!string.IsNullOrEmpty(discipline))
            {
                claims.Add(CreateClaim(DisciplineClaimType, discipline, System.Security.Claims.ClaimValueTypes.String));
            }
        }

        protected override void FillHierarchy(Uri context, string[] entityTypes, string hierarchyNodeID, int numberOfLevels, SPProviderHierarchyTree hierarchy)
        {
            if (!entityTypes.Contains(EntityType) || hierarchyNodeID != null)
            {
                return;
            }

            hierarchy.AddChild(new SPProviderHierarchyNode(ProviderInternalName, "Offices", "offices", true));
            hierarchy.AddChild(new SPProviderHierarchyNode(ProviderInternalName, "Disciplines", "disciplines", true));
        }

        protected override void FillResolve(Uri context, string[] entityTypes, string resolveInput, List<PickerEntity> resolved)
        {
            if (!entityTypes.Contains(EntityType))
            {
                return;
            }

            resolved.AddRange(Candidates().Where(c => Matches(c, resolveInput, true)).Select(c => Entity(c.Item1, c.Item2, c.Item3)));
        }

        protected override void FillResolve(Uri context, string[] entityTypes, SPClaim resolveInput, List<PickerEntity> resolved)
        {
            if (!entityTypes.Contains(EntityType) || resolveInput == null)
            {
                return;
            }

            var match = Candidates().FirstOrDefault(c => c.Item1 == resolveInput.ClaimType && string.Equals(c.Item2, resolveInput.Value, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                resolved.Add(Entity(match.Item1, match.Item2, match.Item3));
            }
        }

        protected override void FillSchema(SPProviderSchema schema)
        {
            schema.AddSchemaElement(new SPSchemaElement(PeopleEditorEntityDataKeys.DisplayName, "Display name", SPSchemaElementType.Both));
        }

        protected override void FillSearch(Uri context, string[] entityTypes, string searchPattern, string hierarchyNodeID, int maxCount, SPProviderHierarchyTree searchTree)
        {
            if (!entityTypes.Contains(EntityType))
            {
                return;
            }

            foreach (var c in Candidates().Where(c => Matches(c, searchPattern, false)).Take(maxCount))
            {
                if (hierarchyNodeID == "offices" && c.Item1 != OfficeClaimType)
                {
                    continue;
                }

                if (hierarchyNodeID == "disciplines" && c.Item1 != DisciplineClaimType)
                {
                    continue;
                }

                searchTree.AddEntity(Entity(c.Item1, c.Item2, c.Item3));
            }
        }

        private static IEnumerable<Tuple<string, string, string>> Candidates()
        {
            yield return Tuple.Create(StaffClaimType, StaffClaimValue, "All Lucerne staff");
            foreach (var o in Office.All)
            {
                yield return Tuple.Create(OfficeClaimType, o.Code, "Office: " + o.Name);
            }

            foreach (var d in Discipline.All)
            {
                yield return Tuple.Create(DisciplineClaimType, d.Code, "Discipline: " + d.Name);
            }
        }

        private static bool Matches(Tuple<string, string, string> candidate, string input, bool exact)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            input = input.Trim();
            return exact
                ? string.Equals(candidate.Item2, input, StringComparison.OrdinalIgnoreCase) || string.Equals(candidate.Item3, input, StringComparison.OrdinalIgnoreCase)
                : candidate.Item3.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 || candidate.Item2.StartsWith(input, StringComparison.OrdinalIgnoreCase);
        }

        private PickerEntity Entity(string claimType, string value, string display)
        {
            var entity = CreatePickerEntity();
            entity.Claim = CreateClaim(claimType, value, System.Security.Claims.ClaimValueTypes.String);
            entity.Description = ProviderDisplayName + ": " + display;
            entity.DisplayText = display;
            entity.EntityType = EntityType;
            entity.IsResolved = true;
            entity.EntityGroupName = ProviderDisplayName;
            entity.EntityData[PeopleEditorEntityDataKeys.DisplayName] = display;
            return entity;
        }
    }

    /// <summary>Farm feature receiver that registers the claims provider with the farm.</summary>
    public class ClaimsProviderFeatureReceiver : SPClaimProviderFeatureReceiver
    {
        public override string ClaimProviderAssembly
        {
            get { return typeof(LucerneClaimProvider).Assembly.FullName; }
        }

        public override string ClaimProviderType
        {
            get { return typeof(LucerneClaimProvider).FullName; }
        }

        public override string ClaimProviderDisplayName
        {
            get { return LucerneClaimProvider.ProviderDisplayName; }
        }

        public override string ClaimProviderDescription
        {
            get { return "Office, discipline and all-staff claims for Lucerne project sites"; }
        }

        public override void FeatureActivated(Microsoft.SharePoint.SPFeatureReceiverProperties properties)
        {
            base.FeatureActivated(properties);
            DirectoryCache.EnsureLocal();
        }
    }
}
