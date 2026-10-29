using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core.Mapping;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Core.Services
{
    /// <summary>Reads and writes the hub site lists (project register, status, staff directory).</summary>
    public class HubRepository
    {
        private readonly ISiteSession _hub;

        public HubRepository(ISiteSession hub)
        {
            _hub = hub;
        }

        public ISiteSession Session
        {
            get { return _hub; }
        }

        public IList<KeyValuePair<int, Project>> GetProjects(params ProjectStage[] stages)
        {
            var query = new CamlQuery { RowLimit = 500 };
            if (stages != null && stages.Length > 0)
            {
                query.Query = Caml.Where(Caml.In(FieldNames.ProjectStage, "Choice", stages.Select(s => (object)s.ToString()).ToArray()))
                    + Caml.OrderBy(FieldNames.ProjectNumber);
            }

            return _hub.GetList(ListNames.Projects).Query(query)
                .Select(i => new KeyValuePair<int, Project>(i.Id, ListMapper.ToProject(i)))
                .ToList();
        }

        public KeyValuePair<int, Project>? FindProject(string number)
        {
            var item = FindByText(ListNames.Projects, FieldNames.ProjectNumber, number);
            return item == null ? (KeyValuePair<int, Project>?)null : new KeyValuePair<int, Project>(item.Id, ListMapper.ToProject(item));
        }

        public ListItemData FindByText(string list, string field, string value)
        {
            return _hub.GetList(list)
                .Query(new CamlQuery { Query = Caml.Where(Caml.Eq(field, "Text", value)), RowLimit = 1, MaxItems = 1 })
                .FirstOrDefault();
        }

        /// <summary>Insert or update by a key text field. Returns true when a new item was added.</summary>
        public bool Upsert(string list, string keyField, string keyValue, FieldValues values)
        {
            var gateway = _hub.GetList(list);
            var existing = FindByText(list, keyField, keyValue);
            if (existing == null)
            {
                gateway.Add(values);
                return true;
            }

            gateway.Update(existing.Id, values, true);
            return false;
        }

        public IDictionary<string, StaffMember> GetStaffByEmployeeId()
        {
            return _hub.GetList(ListNames.StaffDirectory).Query(CamlQuery.All())
                .Select(ListMapper.ToStaff)
                .Where(s => !string.IsNullOrEmpty(s.EmployeeId))
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        public void Log(string list, string title, FieldValues values)
        {
            values[FieldNames.Title] = title;
            _hub.GetList(list).Add(values);
        }
    }
}
