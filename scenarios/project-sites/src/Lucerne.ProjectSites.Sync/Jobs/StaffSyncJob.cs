using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Lucerne.ProjectSites.Contracts.Hr;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Core.Mapping;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Integration.Hr;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Sync.Jobs
{
    /// <summary>HR employees -> hub "Staff Directory". Leavers are kept but marked inactive.</summary>
    public class StaffSyncJob : ISyncJob
    {
        public const string WatermarkName = "hr.employees";

        private readonly IHrDirectory _hr;
        private readonly ISiteConnector _connector;
        private readonly IMapper _mapper;
        private readonly ISyncStateStore _state;
        private readonly SyncOptions _options;
        private readonly IActivityLog _log;

        public StaffSyncJob(IHrDirectory hr, ISiteConnector connector, IMapper mapper, ISyncStateStore state, SyncOptions options, IActivityLog log)
        {
            _hr = hr;
            _connector = connector;
            _mapper = mapper;
            _state = state;
            _options = options;
            _log = log ?? NullActivityLog.Instance;
        }

        public string Name
        {
            get { return "StaffSync"; }
        }

        public SyncRun Run(DateTime nowUtc)
        {
            var run = new SyncRun();
            var watermark = _state.GetWatermark(WatermarkName);
            var since = watermark.HasValue ? watermark.Value - _options.WatermarkOverlap : (DateTime?)null;
            var maxSeen = watermark ?? DateTime.MinValue;

            using (var hubSession = _connector.Open(_options.HubUrl))
            {
                var directory = hubSession.GetList(ListNames.StaffDirectory);
                var existing = directory.Query(CamlQuery.All())
                    .GroupBy(i => i.GetString(FieldNames.EmployeeId) ?? string.Empty)
                    .ToDictionary(g => g.Key, g => g.First().Id);

                foreach (var employee in _hr.GetEmployeesChangedSince(since))
                {
                    try
                    {
                        var member = _mapper.Map<StaffMember>(employee);
                        if (member.LeavingDate.HasValue && member.LeavingDate.Value.Date < nowUtc.Date)
                        {
                            member.Active = false;
                        }

                        if (string.IsNullOrEmpty(member.AccountName))
                        {
                            // Contractors without a domain account are not in the directory.
                            continue;
                        }

                        int id;
                        if (existing.TryGetValue(member.EmployeeId, out id))
                        {
                            directory.Update(id, ListMapper.ToFields(member), true);
                            run.Updated++;
                        }
                        else
                        {
                            existing[member.EmployeeId] = directory.Add(ListMapper.ToFields(member));
                            run.Created++;
                        }

                        if (employee.LastModified > maxSeen)
                        {
                            maxSeen = employee.LastModified;
                        }
                    }
                    catch (Exception ex)
                    {
                        run.Failed++;
                        _log.Error("Employee " + employee.PersonnelNumber + " could not be synchronised", ex);
                    }
                }

                hubSession.Commit();
            }

            if (run.Failed == 0 && maxSeen > DateTime.MinValue)
            {
                _state.SetWatermark(WatermarkName, maxSeen);
            }

            return run;
        }
    }
}
