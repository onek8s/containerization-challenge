using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Data.Repositories
{
    public class ArchiveRepository
    {
        public IList<int> Candidates(DateTime closedBeforeUtc, int batchSize)
        {
            return Db.Query("care.usp_Case_ArchiveCandidates", r => r.GetInt("Id"),
                Db.P("@ClosedBeforeUtc", closedBeforeUtc), Db.P("@BatchSize", batchSize));
        }

        public Case LoadFull(int caseId)
        {
            using (var db = new CareDbContext())
            {
                db.Database.CommandTimeout = 300;
                return db.Cases
                    .Include(c => c.Customer)
                    .Include(c => c.Queue)
                    .Include(c => c.Notes)
                    .Include(c => c.Attachments)
                    .AsNoTracking()
                    .FirstOrDefault(c => c.Id == caseId);
            }
        }

        public void Purge(int caseId, string archiveFile)
        {
            Db.Execute("care.usp_Case_Purge", Db.P("@CaseId", caseId), Db.P("@ArchiveFile", archiveFile));
        }
    }
}
