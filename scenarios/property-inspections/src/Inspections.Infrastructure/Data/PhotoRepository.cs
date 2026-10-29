using System;
using System.Collections.Generic;
using System.Data;
using Northwind.Inspections.Core;

namespace Northwind.Inspections.Infrastructure.Data
{
    public sealed class PhotoRepository
    {
        private const string Columns = "Id, InspectionId, Sequence, OriginalFileName, Caption, StoragePath, ThumbnailPath, Width, Height, Bytes, TakenUtc, UploadedBy, UploadedUtc, Purged";
        private readonly Db db;

        public PhotoRepository(Db db)
        {
            this.db = db;
        }

        public Photo Get(Guid id)
        {
            return db.Single("SELECT " + Columns + " FROM dbo.Photos WHERE Id = @id", Map, Db.P("@id", id));
        }

        public List<Photo> ForInspection(int inspectionId)
        {
            return db.Query("SELECT " + Columns + " FROM dbo.Photos WHERE InspectionId = @i ORDER BY Sequence", Map, Db.P("@i", inspectionId));
        }

        public int NextSequence(int inspectionId)
        {
            return Convert.ToInt32(db.Scalar("SELECT ISNULL(MAX(Sequence), 0) + 1 FROM dbo.Photos WITH (UPDLOCK, HOLDLOCK) WHERE InspectionId = @i", Db.P("@i", inspectionId)));
        }

        public void Insert(Photo p)
        {
            db.Execute(@"INSERT INTO dbo.Photos (" + Columns + @")
VALUES (@id, @i, @seq, @name, @caption, @path, @thumb, @w, @h, @bytes, @taken, @by, @at, 0)",
                Db.P("@id", p.Id), Db.P("@i", p.InspectionId), Db.P("@seq", p.Sequence),
                Db.P("@name", SqlDbType.NVarChar, 260, p.OriginalFileName), Db.P("@caption", SqlDbType.NVarChar, 400, p.Caption),
                Db.P("@path", SqlDbType.NVarChar, 400, p.StoragePath), Db.P("@thumb", SqlDbType.NVarChar, 400, p.ThumbnailPath),
                Db.P("@w", p.Width), Db.P("@h", p.Height), Db.P("@bytes", p.Bytes), Db.P("@taken", p.TakenUtc),
                Db.P("@by", SqlDbType.NVarChar, 128, p.UploadedBy), Db.P("@at", p.UploadedUtc));
        }

        public void UpdateCaption(Guid id, string caption)
        {
            db.Execute("UPDATE dbo.Photos SET Caption = @c WHERE Id = @id", Db.P("@id", id), Db.P("@c", SqlDbType.NVarChar, 400, caption));
        }

        public void Delete(Guid id)
        {
            db.Execute("DELETE FROM dbo.Photos WHERE Id = @id", Db.P("@id", id));
        }

        public void MarkPurged(Guid id)
        {
            db.Execute("UPDATE dbo.Photos SET Purged = 1 WHERE Id = @id", Db.P("@id", id));
        }

        private static Photo Map(IDataRecord r)
        {
            return new Photo
            {
                Id = (Guid)r["Id"],
                InspectionId = (int)r["InspectionId"],
                Sequence = (int)r["Sequence"],
                OriginalFileName = (string)r["OriginalFileName"],
                Caption = Db.Str(r, "Caption"),
                StoragePath = (string)r["StoragePath"],
                ThumbnailPath = (string)r["ThumbnailPath"],
                Width = (int)r["Width"],
                Height = (int)r["Height"],
                Bytes = (long)r["Bytes"],
                TakenUtc = Db.Date(r, "TakenUtc"),
                UploadedBy = (string)r["UploadedBy"],
                UploadedUtc = (DateTime)r["UploadedUtc"],
                Purged = (bool)r["Purged"]
            };
        }
    }
}
