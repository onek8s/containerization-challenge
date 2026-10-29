using System;
using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Erp;
using Lucerne.ProjectSites.LabStubs.Seed;

namespace Lucerne.ProjectSites.LabStubs.Simulators
{
    /// <summary>ERP project accounting service simulator (SOAP over basicHttp via CoreWCF).</summary>
    [CoreWCF.ServiceBehavior(IncludeExceptionDetailInFaults = true, InstanceContextMode = CoreWCF.InstanceContextMode.Single)]
    public class ErpSimulator : IErpProjectService
    {
        private readonly SeedData _seed;

        public ErpSimulator(SeedData seed)
        {
            _seed = seed;
        }

        public int Calls { get; private set; }

        public ErpProjectPage GetProjectsChangedSince(DateTime sinceUtc, int page, int pageSize)
        {
            Calls++;
            if (page < 1 || pageSize < 1 || pageSize > 500)
            {
                throw new CoreWCF.FaultException<ErpFault>(new ErpFault { Code = "ARG", Message = "Invalid paging" }, "Invalid paging");
            }

            var changed = _seed.Projects.Select(p => p.Erp)
                .Where(p => p.LastChangedUtc > sinceUtc.ToUniversalTime())
                .OrderBy(p => p.LastChangedUtc)
                .ToList();
            var items = changed.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new ErpProjectPage
            {
                Projects = items,
                Page = page,
                TotalCount = changed.Count,
                HasMore = page * pageSize < changed.Count,
                ServerTimeUtc = _seed.NowUtc
            };
        }

        public ErpProject GetProject(string projectNumber)
        {
            Calls++;
            var p = _seed.Projects.FirstOrDefault(x => x.Erp.ProjectNo == projectNumber);
            if (p == null)
            {
                throw new CoreWCF.FaultException<ErpFault>(new ErpFault { Code = "NOTFOUND", Message = "Project " + projectNumber + " not found" }, "Not found");
            }

            return p.Erp;
        }

        public List<ErpTimesheetLine> GetApprovedTimesheetLines(DateTime fromDate, DateTime toDate, string projectNumber)
        {
            Calls++;
            return _seed.Timesheets
                .Where(t => t.ProjectNo == projectNumber && t.WorkDate >= fromDate.Date && t.WorkDate <= toDate.Date)
                .ToList();
        }
    }
}
