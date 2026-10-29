using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Domain;

namespace Lucerne.ProjectSites.Core
{
    public class ListDefinition
    {
        public ListDefinition(string title, bool library, string contentTypeId)
        {
            Title = title;
            IsLibrary = library;
            ContentTypeId = contentTypeId;
        }

        public string Title { get; }
        public bool IsLibrary { get; }
        public string ContentTypeId { get; }
    }

    /// <summary>
    /// Lists of the hub and of a project site. The farm creates them from the ListInstance elements and the
    /// project site feature receiver (discipline libraries); this table is what the code relies on.
    /// </summary>
    public static class ProjectSiteSchema
    {
        public const string ArchiveLibrary = "Archive";

        public static IEnumerable<ListDefinition> HubLists()
        {
            yield return new ListDefinition(ListNames.Projects, false, ContentTypeIds.ProjectRecord);
            yield return new ListDefinition(ListNames.ProjectStatus, false, "0x01");
            yield return new ListDefinition(ListNames.SiteRequests, false, ContentTypeIds.SiteRequest);
            yield return new ListDefinition(ListNames.StaffDirectory, false, "0x01");
            yield return new ListDefinition(ListNames.RetentionLog, false, "0x01");
            yield return new ListDefinition(ListNames.SyncLog, false, "0x01");
        }

        public static IEnumerable<ListDefinition> ProjectLists(IEnumerable<string> disciplineCodes)
        {
            yield return new ListDefinition(ListNames.Deliverables, false, ContentTypeIds.Deliverable);
            yield return new ListDefinition(ListNames.Risks, false, ContentTypeIds.Risk);
            yield return new ListDefinition(ListNames.Rfis, false, ContentTypeIds.Rfi);
            yield return new ListDefinition(ListNames.Actions, false, "0x0108");
            yield return new ListDefinition(ListNames.Team, false, "0x01");
            yield return new ListDefinition(ListNames.Hours, false, "0x01");
            yield return new ListDefinition(ListNames.Minutes, true, ContentTypeIds.Correspondence);
            yield return new ListDefinition(ListNames.Transmittals, true, ContentTypeIds.Correspondence);
            yield return new ListDefinition(ArchiveLibrary, true, ContentTypeIds.EngineeringDocument);
            foreach (var d in DisciplineLibraries(disciplineCodes))
            {
                yield return new ListDefinition(d.LibraryName, true, ContentTypeIds.EngineeringDocument);
            }
        }

        /// <summary>Project Management is always there; the other disciplines come from the ERP project.</summary>
        public static IEnumerable<Discipline> DisciplineLibraries(IEnumerable<string> disciplineCodes)
        {
            var codes = new HashSet<string>(disciplineCodes ?? Enumerable.Empty<string>()) { "PMG" };
            return Discipline.All.Where(d => codes.Contains(d.Code));
        }

        public static string OwnersGroup(string projectNumber)
        {
            return projectNumber + " Owners";
        }

        public static string MembersGroup(string projectNumber)
        {
            return projectNumber + " Members";
        }

        public static string VisitorsGroup(string projectNumber)
        {
            return projectNumber + " Visitors";
        }
    }
}
