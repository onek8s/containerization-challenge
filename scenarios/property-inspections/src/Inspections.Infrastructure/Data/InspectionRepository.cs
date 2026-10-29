using System;
using System.Collections.Generic;
using System.Data;
using Northwind.Inspections.Core;

namespace Northwind.Inspections.Infrastructure.Data
{
    public sealed class InspectionRepository
    {
        private const string Select = @"SELECT i.Id, i.ClaimNumber, i.PolicyHolder, i.PropertyAddress, i.PerilCode, i.InspectedOn, i.AdjusterId,
       a.DisplayName AS AdjusterName, i.HandlerEmail, i.Status, i.EstimatedLoss, i.Notes, i.CreatedUtc, i.ReportSentUtc,
       (SELECT COUNT(*) FROM dbo.Photos p WHERE p.InspectionId = i.Id AND p.Purged = 0) AS PhotoCount
FROM dbo.Inspections i JOIN dbo.Adjusters a ON a.Id = i.AdjusterId ";

        private readonly Db db;

        public InspectionRepository(Db db)
        {
            this.db = db;
        }

        public Inspection Get(int id)
        {
            return db.Single(Select + "WHERE i.Id = @id", Map, Db.P("@id", id));
        }

        public List<Inspection> ForAdjuster(int adjusterId, bool includeClosed)
        {
            return db.Query(Select + "WHERE i.AdjusterId = @a" + (includeClosed ? string.Empty : " AND i.Status < 4") + " ORDER BY i.InspectedOn DESC, i.Id DESC",
                Map, Db.P("@a", adjusterId));
        }

        public List<Inspection> Recent(int top)
        {
            return db.Query("SELECT TOP (@top) * FROM (" + Select + ") x ORDER BY x.CreatedUtc DESC", Map, Db.P("@top", top));
        }

        public List<Inspection> ForClaim(string claimNumber)
        {
            return db.Query(Select + "WHERE i.ClaimNumber = @c ORDER BY i.InspectedOn", Map, Db.P("@c", SqlDbType.VarChar, 20, claimNumber));
        }

        public List<Inspection> PurgeCandidates()
        {
            return db.Query(Select + "WHERE i.Status IN (3, 4) AND EXISTS (SELECT 1 FROM dbo.Photos p WHERE p.InspectionId = i.Id AND p.Purged = 0)", Map);
        }

        public int Create(Inspection inspection, string actor)
        {
            var id = db.Scalar(@"INSERT INTO dbo.Inspections (ClaimNumber, PolicyHolder, PropertyAddress, PerilCode, InspectedOn, AdjusterId, HandlerEmail, Status, EstimatedLoss, Notes, CreatedUtc)
OUTPUT inserted.Id
VALUES (@claim, @holder, @address, @peril, @on, @adjuster, @handler, 0, @loss, @notes, SYSUTCDATETIME())",
                Db.P("@claim", SqlDbType.VarChar, 20, inspection.ClaimNumber),
                Db.P("@holder", SqlDbType.NVarChar, 200, inspection.PolicyHolder),
                Db.P("@address", SqlDbType.NVarChar, 400, inspection.PropertyAddress),
                Db.P("@peril", SqlDbType.Char, 3, inspection.PerilCode),
                Db.P("@on", SqlDbType.Date, 0, inspection.InspectedOn.Date),
                Db.P("@adjuster", inspection.AdjusterId),
                Db.P("@handler", SqlDbType.NVarChar, 256, inspection.HandlerEmail),
                Db.P("@loss", SqlDbType.Money, 0, inspection.EstimatedLoss),
                Db.P("@notes", SqlDbType.NVarChar, -1, inspection.Notes));
            var newId = Convert.ToInt32(id);
            Audit(newId, "Created", actor, inspection.ClaimNumber);
            return newId;
        }

        public void Update(Inspection inspection, string actor)
        {
            db.Execute(@"UPDATE dbo.Inspections SET PolicyHolder = @holder, PropertyAddress = @address, PerilCode = @peril, InspectedOn = @on,
    HandlerEmail = @handler, EstimatedLoss = @loss, Notes = @notes WHERE Id = @id",
                Db.P("@id", inspection.Id),
                Db.P("@holder", SqlDbType.NVarChar, 200, inspection.PolicyHolder),
                Db.P("@address", SqlDbType.NVarChar, 400, inspection.PropertyAddress),
                Db.P("@peril", SqlDbType.Char, 3, inspection.PerilCode),
                Db.P("@on", SqlDbType.Date, 0, inspection.InspectedOn.Date),
                Db.P("@handler", SqlDbType.NVarChar, 256, inspection.HandlerEmail),
                Db.P("@loss", SqlDbType.Money, 0, inspection.EstimatedLoss),
                Db.P("@notes", SqlDbType.NVarChar, -1, inspection.Notes));
            Audit(inspection.Id, "Updated", actor, null);
        }

        public void SetStatus(int id, InspectionStatus status, string actor)
        {
            db.Execute("UPDATE dbo.Inspections SET Status = @s, ReportSentUtc = CASE WHEN @s = 3 THEN SYSUTCDATETIME() ELSE ReportSentUtc END WHERE Id = @id",
                Db.P("@id", id), Db.P("@s", (byte)status));
            Audit(id, "Status " + status, actor, null);
        }

        public void Audit(int inspectionId, string action, string actor, string detail)
        {
            db.Execute("INSERT INTO dbo.AuditLog (InspectionId, Action, Actor, AtUtc, Detail) VALUES (@i, @a, @actor, SYSUTCDATETIME(), @d)",
                Db.P("@i", inspectionId), Db.P("@a", SqlDbType.NVarChar, 100, action), Db.P("@actor", SqlDbType.NVarChar, 128, actor),
                Db.P("@d", SqlDbType.NVarChar, 1000, detail));
        }

        public List<KeyValuePair<DateTime, string>> History(int inspectionId)
        {
            return db.Query("SELECT AtUtc, Actor + ': ' + Action + ISNULL(' (' + Detail + ')', '') AS Line FROM dbo.AuditLog WHERE InspectionId = @i ORDER BY AtUtc DESC",
                r => new KeyValuePair<DateTime, string>((DateTime)r["AtUtc"], (string)r["Line"]), Db.P("@i", inspectionId));
        }

        private static Inspection Map(IDataRecord r)
        {
            return new Inspection
            {
                Id = (int)r["Id"],
                ClaimNumber = (string)r["ClaimNumber"],
                PolicyHolder = (string)r["PolicyHolder"],
                PropertyAddress = (string)r["PropertyAddress"],
                PerilCode = Db.Str(r, "PerilCode"),
                InspectedOn = (DateTime)r["InspectedOn"],
                AdjusterId = (int)r["AdjusterId"],
                AdjusterName = (string)r["AdjusterName"],
                HandlerEmail = Db.Str(r, "HandlerEmail"),
                Status = (InspectionStatus)(byte)r["Status"],
                EstimatedLoss = Db.Dec(r, "EstimatedLoss"),
                Notes = Db.Str(r, "Notes"),
                CreatedUtc = (DateTime)r["CreatedUtc"],
                ReportSentUtc = Db.Date(r, "ReportSentUtc"),
                PhotoCount = (int)r["PhotoCount"]
            };
        }
    }
}
